
using System.Security.Claims;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentInventoryApp.Controllers
{
    // Employees request reservations and see their requests (US-23, US-25).
    // Administrators approve or deny them (US-24, FR-26, FR-27).
    [Authorize]
    public class ReservationsController : Controller
    {
        // The details page section that holds the Request Reservation form
        private const string FormAnchor = "request-reservation";

        private readonly ReservationService _reservations;

        public ReservationsController(ReservationService reservations)
        {
            _reservations = reservations;
        }

        // Saves a request from the details page as Pending, then opens My Reservations
        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReservationRequestViewModel model)
        {
            var result = await _reservations.RequestAsync(
                model.EquipmentId,
                GetUserId(),
                model.StartDate,
                model.EndDate);

            if (!result.Succeeded)
            {
                // Shows the message and the dates that were entered on the details page
                TempData["ErrorMessage"] = result.Message;
                // Keeps the form open; an empty value still opens it
                TempData["ReservationStartDraft"] = model.StartDate?.ToString("yyyy-MM-dd") ?? "";
                TempData["ReservationEndDraft"] = model.EndDate?.ToString("yyyy-MM-dd") ?? "";

                return Redirect(Url.Action("Details", "Equipment", new { id = model.EquipmentId }) + "#" + FormAnchor);
            }

            TempData["SuccessMessage"] = result.Message;

            return RedirectToAction(nameof(Mine));
        }

        // My Reservations: the employee's requests with item, dates, and status (FR-29)
        [HttpGet]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Mine(int page = 1)
        {
            var model = await _reservations.GetForUserAsync(GetUserId(), page);

            return View(model);
        }

        // Cancels one of the employee's own Pending or Approved requests
        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMine(int id, int page = 1)
        {
            var result = await _reservations.CancelByEmployeeAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Mine), new { page });
        }

        // Lists Pending requests with Approve and Deny buttons (FR-26),
        // and Approved reservations that have not ended, with Cancel buttons
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Index(int page = 1, int approvedPage = 1)
        {
            var model = await _reservations.GetPendingAsync(page);

            ViewBag.Approved = await _reservations.GetApprovedAsync(approvedPage);

            return View(model);
        }

        // Cancels an Approved reservation and notifies the employee
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, int page = 1, int approvedPage = 1)
        {
            var result = await _reservations.CancelByAdministratorAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return Redirect(Url.Action(nameof(Index), new { page, approvedPage }) + "#approved-reservations");
        }

        // Approves a request unless it overlaps an Approved reservation (FR-27)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int page = 1)
        {
            var result = await _reservations.ApproveAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Index), new { page });
        }

        // Denies a request
        [HttpPost]
        [Authorize(Roles = "Administrator")]
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
