
using Microsoft.AspNetCore.Authorization;
using EquipmentInventoryApp.Data;
using System.Security.Claims;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.Services;
using EquipmentInventoryApp.ViewModels;
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
        private readonly IWebHostEnvironment _environment;
        private readonly CheckoutService _checkoutService;

        // Photo rules from the requirements: JPG or PNG, 5 MB or smaller
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private const string PhotoErrorMessage = "Photo must be a JPG or PNG under 5 MB.";

        public EquipmentController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            CheckoutService checkoutService)
        {
            _context = context;
            _environment = environment;
            _checkoutService = checkoutService;
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

            // Items reserved for today are shown as Reserved instead of Available (FR-28)
            var reservedIds = _context.ReservedEquipmentIds(DateTime.Today);

            // Filters equipment by the status users see
            if (status == "Reserved")
            {
                equipment = equipment.Where(e =>
                    e.Status == "Reserved" ||
                    (e.Status == "Available" && reservedIds.Contains(e.EquipmentId)));
            }
            else if (status == "Available")
            {
                equipment = equipment.Where(e =>
                    e.Status == "Available" &&
                    !reservedIds.Contains(e.EquipmentId));
            }
            else if (!string.IsNullOrWhiteSpace(status))
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
                EquipmentOptions.Statuses,
                status);

            ViewBag.ReservedIds = (await reservedIds.ToListAsync()).ToHashSet();

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

            // Shows Reserved while an Approved reservation covers today (FR-28)
            var today = DateTime.Today;

            var reservationHolders = await _context.Reservations
                .Where(r => r.EquipmentId == equipment.EquipmentId &&
                            r.Status == "Approved" &&
                            r.StartDate <= today &&
                            r.EndDate >= today)
                .Select(r => r.UserId)
                .ToListAsync();

            ViewBag.DisplayStatus = EquipmentOptions.DisplayStatus(
                equipment.Status,
                reservationHolders.Any());

            // Employees see a Check Out button that is enabled only when they can use it
            if (User.IsInRole("Employee"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                ViewBag.ReservedForCurrentUser = reservationHolders.Contains(userId);

                ViewBag.CheckoutAvailability =
                    await _checkoutService.GetAvailabilityAsync(equipment, userId);
            }

            return View(equipment);
        }

        // Opens the Add Equipment form
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create()
        {
            var model = new EquipmentFormViewModel();

            await LoadFormOptionsAsync(model);

            return View(model);
        }

        // Checks and saves a new equipment item
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EquipmentFormViewModel model)
        {
            var name = model.Name?.Trim() ?? string.Empty;
            var serialNumber = model.SerialNumber?.Trim() ?? string.Empty;

            Category? newCategory = null;

            if (model.CategoryId == EquipmentFormViewModel.NewCategoryOption)
            {
                // Checks the name typed for a new category
                var categoryName = model.NewCategoryName?.Trim() ?? string.Empty;

                if (categoryName.Length == 0)
                {
                    ModelState.AddModelError(
                        nameof(model.NewCategoryName),
                        "Please enter a category name");
                }
                else if (categoryName.Length > 100)
                {
                    ModelState.AddModelError(
                        nameof(model.NewCategoryName),
                        "Category name must be 100 characters or fewer");
                }
                else if (await CategoryExistsAsync(categoryName))
                {
                    ModelState.AddModelError(
                        nameof(model.NewCategoryName),
                        "Category already exists.");
                }
                else
                {
                    newCategory = new Category { CategoryName = categoryName };
                }
            }
            else if (model.CategoryId.HasValue &&
                !await _context.Categories.AnyAsync(c =>
                    c.CategoryId == model.CategoryId.Value))
            {
                // Makes sure the chosen category still exists
                ModelState.AddModelError(
                    nameof(model.CategoryId),
                    "Please select a category");
            }

            // Only allows the choices listed in the requirements
            if (!string.IsNullOrWhiteSpace(model.Condition) &&
                !EquipmentOptions.Conditions.Contains(model.Condition))
            {
                ModelState.AddModelError(
                    nameof(model.Condition),
                    "Please select a valid condition");
            }

            if (!EquipmentOptions.Statuses.Contains(model.Status))
            {
                ModelState.AddModelError(
                    nameof(model.Status),
                    "Please select a valid status");
            }

            // Serial numbers must be unique
            if (serialNumber.Length > 0 &&
                await _context.Equipment.AnyAsync(e =>
                    e.SerialNumber == serialNumber))
            {
                ModelState.AddModelError(
                    nameof(model.SerialNumber),
                    "This serial number is already assigned to another item");
            }

            // Checks the photo type and size
            if (model.Photo != null && !await IsValidPhotoAsync(model.Photo))
            {
                ModelState.AddModelError(
                    nameof(model.Photo),
                    PhotoErrorMessage);
            }

            if (!ModelState.IsValid)
            {
                // Browsers cannot refill a file box, so the user is asked to pick it again
                ViewBag.ReselectPhoto = model.Photo != null &&
                    !ModelState.ContainsKey(nameof(model.Photo));

                await LoadFormOptionsAsync(model);

                return View(model);
            }

            string? photoPath = null;

            if (model.Photo != null)
            {
                photoPath = await SavePhotoAsync(model.Photo);
            }

            var equipment = new Equipment
            {
                Name = name,
                SerialNumber = serialNumber,
                Location = string.IsNullOrWhiteSpace(model.Location)
                    ? null
                    : model.Location.Trim(),
                Condition = string.IsNullOrWhiteSpace(model.Condition)
                    ? null
                    : model.Condition,
                Status = model.Status,
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? null
                    : model.Description.Trim(),
                PhotoPath = photoPath
            };

            // Saves a new category together with the item, or uses the chosen one
            if (newCategory != null)
            {
                equipment.Category = newCategory;
            }
            else
            {
                equipment.CategoryId = model.CategoryId!.Value;
            }

            _context.Equipment.Add(equipment);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Removes the saved photo if the item could not be saved
                DeletePhoto(photoPath);

                _context.Entry(equipment).State = EntityState.Detached;

                if (newCategory != null)
                {
                    _context.Entry(newCategory).State = EntityState.Detached;
                }

                // Another administrator may have used the same serial number
                // or category name at the same moment
                var serialTaken = await _context.Equipment.AnyAsync(e =>
                    e.SerialNumber == serialNumber);

                var categoryTaken = newCategory != null &&
                    await CategoryExistsAsync(newCategory.CategoryName);

                if (!serialTaken && !categoryTaken)
                {
                    throw;
                }

                if (serialTaken)
                {
                    ModelState.AddModelError(
                        nameof(model.SerialNumber),
                        "This serial number is already assigned to another item");
                }

                if (categoryTaken)
                {
                    ModelState.AddModelError(
                        nameof(model.NewCategoryName),
                        "Category already exists.");
                }

                await LoadFormOptionsAsync(model);

                return View(model);
            }

            TempData["SuccessMessage"] = "Equipment added.";

            return RedirectToAction(
                nameof(Details),
                new { id = equipment.EquipmentId });
        }

        // Checks for a category with the same name, ignoring capitalization
        private Task<bool> CategoryExistsAsync(string categoryName)
        {
            var lowerName = categoryName.ToLower();

            return _context.Categories.AnyAsync(c =>
                c.CategoryName.ToLower() == lowerName);
        }

        // Fills the dropdown lists on the equipment form
        private async Task LoadFormOptionsAsync(EquipmentFormViewModel model)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var categoryItems = categories
                .Select(c => new SelectListItem(
                    c.CategoryName,
                    c.CategoryId.ToString()))
                .ToList();

            // The last choice lets the user type a new category
            categoryItems.Add(new SelectListItem(
                "+ Add New",
                EquipmentFormViewModel.NewCategoryOption.ToString()));

            model.Categories = categoryItems;

            model.Conditions = new SelectList(
                EquipmentOptions.Conditions,
                model.Condition);

            model.Statuses = new SelectList(
                EquipmentOptions.Statuses,
                model.Status);
        }

        // Checks that a photo is a real JPG or PNG file and is not too large
        private static async Task<bool> IsValidPhotoAsync(IFormFile photo)
        {
            if (photo.Length == 0 || photo.Length > MaxPhotoBytes)
            {
                return false;
            }

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();

            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return false;
            }

            // Reads the start of the file so renamed files are rejected
            var header = new byte[8];

            using var stream = photo.OpenReadStream();

            var bytesRead = await stream.ReadAtLeastAsync(
                header,
                header.Length,
                throwOnEndOfStream: false);

            var isJpg = bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            var isPng = bytesRead >= 8 &&
                header.SequenceEqual(new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
                });

            return extension == ".png" ? isPng : isJpg;
        }

        // Saves a photo under a new file name and returns its web path
        private async Task<string> SavePhotoAsync(IFormFile photo)
        {
            var folder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "equipment");

            Directory.CreateDirectory(folder);

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();

            if (extension == ".jpeg")
            {
                extension = ".jpg";
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";

            using (var stream = new FileStream(
                Path.Combine(folder, fileName),
                FileMode.CreateNew))
            {
                await photo.CopyToAsync(stream);
            }

            return $"/uploads/equipment/{fileName}";
        }

        // Deletes a saved photo file
        private void DeletePhoto(string? photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return;
            }

            var fullPath = Path.Combine(
                _environment.WebRootPath,
                photoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
