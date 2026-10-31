
using EquipmentInventoryApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EquipmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? searchString,
            int? categoryId,
            string? status)
        {
            var equipment = _context.Equipment
                .Include(e => e.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                equipment = equipment.Where(e =>
                    e.Name.Contains(searchString) ||
                    e.SerialNumber.Contains(searchString));
            }

            if (categoryId.HasValue)
            {
                equipment = equipment.Where(e =>
                    e.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                equipment = equipment.Where(e =>
                    e.Status == status);
            }

            ViewBag.SearchString = searchString;
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status;

            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .OrderBy(c => c.CategoryName)
                    .ToListAsync(),
                "CategoryId",
                "CategoryName",
                categoryId);

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

            if (equipment == null)
            {
                return NotFound();
            }

            return View(equipment);
        }
    }
}
