
using System.Security.Claims;
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    // Employees report damage; administrators review it (US-29, US-30)
    [Authorize]
    public class DamageReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DamageReportService _damageReports;

        public DamageReportsController(
            ApplicationDbContext context,
            DamageReportService damageReports)
        {
            _context = context;
            _damageReports = damageReports;
        }

        // Opens the Report Damage form for an item
        [HttpGet]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(int equipmentId)
        {
            var model = await LoadFormAsync(equipmentId, new DamageReportFormViewModel());

            if (model == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction("Index", "Equipment");
            }

            return View(model);
        }

        // Saves the report as Open and notifies the administrators
        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DamageReportFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return await ShowFormAgainAsync(model);
            }

            var result = await _damageReports.SubmitAsync(
                model.EquipmentId,
                GetUserId(),
                model.Description);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(nameof(model.Description), result.Message);

                return await ShowFormAgainAsync(model);
            }

            TempData["SuccessMessage"] = result.Message;

            return RedirectToAction("Details", "Equipment", new { id = model.EquipmentId });
        }

        // Lists the open damage reports (FR-33)
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Index(int page = 1)
        {
            var model = await _damageReports.GetOpenAsync(page);

            return View(model);
        }

        // Sets the reported item to Under Repair (FR-34)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnderRepair(int id, int page = 1)
        {
            var result = await _damageReports.MarkUnderRepairAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Index), new { page });
        }

        // Closes the report and records the closing date (FR-35)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, int page = 1)
        {
            var result = await _damageReports.CloseAsync(id, GetUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Index), new { page });
        }

        // Fills in the item name and serial number shown on the form
        private async Task<DamageReportFormViewModel?> LoadFormAsync(
            int equipmentId,
            DamageReportFormViewModel model)
        {
            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return null;
            }

            model.EquipmentId = equipment.EquipmentId;
            model.ItemName = equipment.Name;
            model.SerialNumber = equipment.SerialNumber;

            return model;
        }

        private async Task<IActionResult> ShowFormAgainAsync(DamageReportFormViewModel model)
        {
            if (await LoadFormAsync(model.EquipmentId, model) == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction("Index", "Equipment");
            }

            return View("Create", model);
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
