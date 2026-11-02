using System.Diagnostics;
using EquipmentInventoryApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentInventoryApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // The site opens on the login page. Users who are already logged in
        // are sent to the page for their role instead.
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            if (User.IsInRole("Administrator"))
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "Equipment");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
