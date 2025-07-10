using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using DemoSRP;
using DemoSRP.Models;
using System.Text.RegularExpressions;
using AngleSharp.Io;

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
            // Validate username
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new
                {
                    Error = "failed",
                    Message = "BadRequest"
                });
            }

            if (!Regex.IsMatch(request.Username, @"^[a-zA-Z0-9]{4,10}$"))
            {
                return BadRequest(new
                {
                    Error = "failed",
                    Message = "BadRequest"
                });
            }

            if (_userDatabase.UserExists(request.Username))
            {
                return BadRequest(new
                {
                    Error = "failed",
                    Message = "BadRequest"
                });
            }

            try
            {
                BigInteger salt = BigInteger.Parse(request.Salt);
                BigInteger verifier = BigInteger.Parse(request.Verifier);
                _userDatabase.RegisterUser(request.Username, salt, verifier);
                return Ok(new { message = "Регистрация успешна" });
            }
            catch (Exception)
            {
                return BadRequest(new
                {
                    Error = "failed",
                    Message = $"BadRequest"
                });
            }
        }
    }
}