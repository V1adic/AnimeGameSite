using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using Microsoft.AspNetCore.Http;
using DemoSRP;
using SRP;
using DemoSRP.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace DemoSRP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly UserDatabase _userDatabase;
        private readonly string _connectionString;
        private const string ServerBKey = "ServerB";
        private const string ServerSaltKey = "ServerSalt";
        private const string ServerUsernameKey = "ServerUsername";
        private const string ServerVerifierKey = "ServerVerifier";
        private const string ServerPrivateBKey = "ServerPrivateB";

        public LoginController(UserDatabase userDatabase)
        {
            _userDatabase = userDatabase;
            _connectionString = "Data Source=users.db";
        }

        [HttpPost("start")]
        public IActionResult StartLogin([FromBody] LoginRequest request)
        {
            var userData = _userDatabase.GetUserData(request.Username);

            BigInteger dummySalt = BigInteger.Parse("12345678901234567890123456789012");
            BigInteger dummyVerifier = BigInteger.Parse("9876543210987654321098765432109876543210");

            BigInteger salt = userData.HasValue ? userData.Value.Salt : dummySalt;
            BigInteger verifier = userData.HasValue ? userData.Value.Verifier : dummyVerifier;

            var server = new SrpServer(request.Username, salt, verifier);
            var (B, returnedSalt) = server.GeneratePublicKeyAndSalt();

            HttpContext.Session.SetString(ServerBKey, B.ToString());
            HttpContext.Session.SetString(ServerSaltKey, returnedSalt.ToString());
            HttpContext.Session.SetString(ServerUsernameKey, request.Username);
            HttpContext.Session.SetString(ServerVerifierKey, verifier.ToString());
            HttpContext.Session.SetString(ServerPrivateBKey, typeof(SrpServer)
                .GetField("_b", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(server).ToString());

            server.ComputeSessionKey(BigInteger.Parse(request.A));

            return Ok(new LoginResponse
            {
                B = B.ToString(),
                Salt = returnedSalt.ToString()
            });
        }

        [HttpPost("verify")]
        public IActionResult VerifyClient([FromBody] ClientProofRequest request)
        {
            var B = HttpContext.Session.GetString(ServerBKey);
            var salt = HttpContext.Session.GetString(ServerSaltKey);
            var username = HttpContext.Session.GetString(ServerUsernameKey);
            var verifier = HttpContext.Session.GetString(ServerVerifierKey);
            var privateB = HttpContext.Session.GetString(ServerPrivateBKey);

            if (string.IsNullOrEmpty(B) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(username) ||
                string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(privateB))
            {
                return BadRequest(new { Message = "Session not started" });
            }

            var server = new SrpServer(username, BigInteger.Parse(salt), BigInteger.Parse(verifier));
            typeof(SrpServer).GetField("_B", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(server, BigInteger.Parse(B));
            typeof(SrpServer).GetField("_b", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(server, BigInteger.Parse(privateB));

            var parts = request.M1.Split('|');
            if (parts.Length != 2) return BadRequest(new { Message = "Invalid M1 format" });
            var A = BigInteger.Parse(parts[0]);
            var M1 = BigInteger.Parse(parts[1]);

            var sessionKey = server.ComputeSessionKey(A);

            if (!server.VerifyClientProof(A, M1))
            {
                return BadRequest(new { Message = "Client proof verification failed" });
            }

            var M2 = server.ComputeServerProof(A, M1);
            var token = GenerateJwtToken(username);

            // Логирование успешного входа
            _userDatabase.LogAuthEvent(username, "Login");

            var IV = RandomNumberGenerator.GetBytes(16);
            var encryptToken = EncryptStringToBytes_Aes(token, sessionKey, IV);

            string tokenBase64 = Convert.ToBase64String(encryptToken);
            string ivBase64 = Convert.ToBase64String(IV);

            return Ok(new ServerProofResponse
            {
                M2 = M2.ToString(),
                Token = tokenBase64,
                IV = ivBase64
            });
        }

        private string GenerateJwtToken(string username)
        {
            var userData = _userDatabase.GetUserData(username);
            var role = userData.HasValue ? userData.Value.Role : "User";

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                ControllerContext.HttpContext.RequestServices.GetService<IConfiguration>()["Jwt:Key"]
                ?? "YourSuperSecretKey12345678901234567890"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "QuietPlanet",
                audience: "QuietPlanetUsers",
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            string? username = null;
            var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);
                    username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                }
                catch
                {
                    // Игнорируем ошибки парсинга токена, логирование не обязательно
                }
            }

            if (!string.IsNullOrEmpty(username))
            {
                _userDatabase.LogAuthEvent(username, "Logout");
            }

            // Очистка серверной сессии
            HttpContext.Session.Clear();

            return Ok(new { Message = "Выход успешен, токен удален." });
        }

        static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException(nameof(Key));
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException(nameof(IV));
            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using MemoryStream msEncrypt = new();
                using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
                using (StreamWriter swEncrypt = new(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
                encrypted = msEncrypt.ToArray();
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }
    }
}