
using EquipmentInventoryApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentInventoryApp.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly DashboardService _dashboard;

        public AdminController(DashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        // Opens the administrator dashboard. The counts are queried from the database
        // on every page load (FR-39), and the browser is told not to keep an old copy.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index()
        {
            var model = await _dashboard.GetDashboardAsync();

            return View(model);
        }

        // Lists every overdue checkout, most overdue first (US-35, FR-41)
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Overdue(int page = 1)
        {
            var model = await _dashboard.GetOverdueListAsync(page);

            return View(model);
        }
    }
}
