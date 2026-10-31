
using Microsoft.AspNetCore.Authorization;
using EquipmentInventoryApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    // Allows employees and administrators to view equipment
    [Authorize(Roles = "Employee,Administrator")]
    public class EquipmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EquipmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Gets the equipment list from the database
        public async Task<IActionResult> Index(
            string? searchString,
            int? categoryId,
            string? status)
        {
            var equipment = _context.Equipment
                .Include(e => e.Category)
                .AsNoTracking()
                .AsQueryable();

            // Searches equipment by name or serial number
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                equipment = equipment.Where(e =>
                    e.Name.Contains(searchString) ||
                    e.SerialNumber.Contains(searchString));
            }

            // Filters equipment by category
            if (categoryId.HasValue)
            {
                equipment = equipment.Where(e =>
                    e.CategoryId == categoryId.Value);
            }

            // Filters equipment by its current status
            if (!string.IsNullOrWhiteSpace(status))
            {
                equipment = equipment.Where(e =>
                    e.Status == status);
            }

            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status;

            // Gets the categories for the dropdown menu
            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .OrderBy(c => c.CategoryName)
                    .ToListAsync(),
                "CategoryId",
                "CategoryName",
                categoryId);

            // Sets up the available status filters
            ViewBag.Statuses = new SelectList(
                new[]
                {
                    "Available",
                    "Checked Out",
                    "Under Repair",
                    "Lost",
                    "Retired"
                },
                status);

            return View(await equipment
                .OrderBy(e => e.Name)
                .ToListAsync());
        }

        // Gets the details for one equipment item
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var equipment = await _context.Equipment
                .Include(e => e.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            // Returns an error if the equipment is not found
            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }
    }
}
