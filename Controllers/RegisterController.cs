using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using DemoSRP;
using DemoSRP.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DemoSRP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegisterController : ControllerBase
    {
        private readonly UserDatabase _userDatabase;

        public RegisterController(UserDatabase userDatabase)
        {
            _userDatabase = userDatabase;
        }

        [HttpPost]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (_userDatabase.UserExists(request.Username))
                return BadRequest(new { Message = "Пользователь уже существует." });

            try
            {
                BigInteger salt = BigInteger.Parse(request.Salt);
                BigInteger verifier = BigInteger.Parse(request.Verifier);
                _userDatabase.RegisterUser(request.Username, salt, verifier);
                return Ok(new { message = "Регистрация успешна" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = $"Ошибка регистрации: {ex.Message}" });
            }
        }
    }
}