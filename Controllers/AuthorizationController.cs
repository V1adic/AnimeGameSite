using Microsoft.AspNetCore.Mvc;

namespace AnimeGameSite.Controllers
{
    public class AuthorizationController : Controller
    {
        [HttpGet("/login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpGet("/register")]
        public IActionResult Register()
        {
            return View();
        }
    }
}
