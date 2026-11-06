
using System.Security.Claims;
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    // Employee checkout actions and the My Equipment page
    [Authorize(Roles = "Employee")]
    public class CheckoutsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CheckoutService _checkoutService;
        private readonly CheckoutHistoryService _checkoutHistory;

        public CheckoutsController(
            ApplicationDbContext context,
            CheckoutService checkoutService,
            CheckoutHistoryService checkoutHistory)
        {
            _context = context;
            _checkoutService = checkoutService;
            _checkoutHistory = checkoutHistory;
        }

        // Checks out an item from its details page (US-18)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int id)
        {
            var result = await _checkoutService.CheckOutAsync(id, GetUserId());

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = result.Message;

                return RedirectToAction("Details", "Equipment", new { id });
            }

            // Shows the item and its due date on My Equipment (US-20).
            // A repeated click adds no second message.
            if (!result.WasRepeat)
            {
                TempData["SuccessMessage"] = result.Message;
            }

            return RedirectToAction(nameof(MyEquipment));
        }

        // Returns an item from My Equipment (FR-24, FR-25)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id)
        {
            var result = await _checkoutService.ReturnAsync(id, GetUserId());

            // A repeated click adds no second message
            if (!result.WasRepeat)
            {
                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(MyEquipment));
        }

        // Lists the items the employee has checked out now (FR-30, FR-31)
        [HttpGet]
        public async Task<IActionResult> MyEquipment()
        {
            var userId = GetUserId();
            var today = DateTime.Today;

            var checkouts = await _context.Checkouts
                .Include(c => c.Equipment)
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.ReturnDate == null)
                .OrderBy(c => c.DueDate)
                .ToListAsync();

            var model = new MyEquipmentViewModel
            {
                MaxActiveCheckouts = _checkoutService.MaxActiveCheckouts,
                Items = checkouts
                    .Select(c => new MyEquipmentItemViewModel
                    {
                        CheckoutId = c.CheckoutId,
                        EquipmentId = c.EquipmentId,
                        Name = c.Equipment.Name,
                        SerialNumber = c.Equipment.SerialNumber,
                        CheckoutDate = c.CheckoutDate,
                        DueDate = c.DueDate,
                        IsOverdue = c.DueDate.Date < today
                    })
                    .ToList()
            };

            return View(model);
        }

        // Lists every item the employee has checked out, past and current, newest first (US-27)
        [HttpGet]
        public async Task<IActionResult> History(int page = 1)
        {
            var model = await _checkoutHistory.GetForUserAsync(GetUserId(), page);

            return View(model);
        }

        // Gets the logged-in user's ID from their login cookie
        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
