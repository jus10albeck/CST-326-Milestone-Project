
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Opens the administrator dashboard
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // Counts equipment by its current status
            var model = new AdminDashboardViewModel
            {
                CheckedOutCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Checked Out"),

                UnderRepairCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Under Repair"),

                LostCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Lost")
            };

            // Finds checkouts that have not been returned and are past due
            var overdueCheckouts = await _context.Checkouts
                .Include(c => c.Equipment)
                .Include(c => c.User)
                .AsNoTracking()
                .Where(c => c.ReturnDate == null && c.DueDate < today)
                .OrderBy(c => c.DueDate)
                .ToListAsync();

            model.OverdueItems = overdueCheckouts
                .Select(c => new OverdueItemViewModel
                {
                    EquipmentId = c.EquipmentId,
                    ItemName = c.Equipment.Name,
                    EmployeeName = $"{c.User.FirstName} {c.User.LastName}",
                    DueDate = c.DueDate,
                    DaysOverdue = (today - c.DueDate.Date).Days
                })
                .ToList();

            model.OverdueCount = model.OverdueItems.Count;

            return View(model);
        }
    }
}
