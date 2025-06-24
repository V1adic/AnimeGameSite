using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnimeGameSite.Controllers
{
    [Route("donat")]
    public class DonatController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/Home/Donat.cshtml");
        }

        [Authorize(AuthenticationSchemes = "Bearer", Roles = "Donator,Admin")]
        [HttpGet("secure")]
        public IActionResult Secure()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge("Bearer");
            }
            return Ok(new { Message = "Secure donator endpoint accessed successfully", User = User.Identity.Name });
        }
    }
}