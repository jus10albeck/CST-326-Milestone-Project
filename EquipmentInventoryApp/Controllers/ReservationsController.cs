
using System.Security.Claims;
using EquipmentInventoryApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentInventoryApp.Controllers
{
    // Administrators approve or deny reservation requests (US-24, FR-26, FR-27)
    [Authorize(Roles = "Administrator")]
    public class ReservationsController : Controller
    {
        private readonly ReservationService _reservations;

        public ReservationsController(ReservationService reservations)
        {
            _reservations = reservations;
        }

        // Lists Pending requests with Approve and Deny buttons (FR-26)
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            var model = await _reservations.GetPendingAsync(page);

            return View(model);
        }

        // Approves a request unless it overlaps an Approved reservation (FR-27)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int page = 1)
        {
            var result = await _reservations.ApproveAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Index), new { page });
        }

        // Denies a request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deny(int id, int page = 1)
        {
            var result = await _reservations.DenyAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Index), new { page });
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
