using Microsoft.AspNetCore.Mvc;

namespace AnimeGameSite.Controllers
{
    public class DonationController : Controller
    {
        public IActionResult Index()
        {
            return View("Views/Home/Donation.cshtml");
        }
    }
}
