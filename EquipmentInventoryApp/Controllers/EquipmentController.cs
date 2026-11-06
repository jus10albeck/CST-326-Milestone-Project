
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
        private readonly EquipmentStatusService _statusService;
        private readonly EquipmentHistoryService _history;
        private readonly CheckoutHistoryService _checkoutHistory;

        // Photo rules from the requirements: JPG or PNG, 5 MB or smaller
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private const string PhotoErrorMessage = "Photo must be a JPG or PNG under 5 MB.";

        public EquipmentController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            CheckoutService checkoutService,
            EquipmentStatusService statusService,
            EquipmentHistoryService history,
            CheckoutHistoryService checkoutHistory)
        {
            _history = history;
            _checkoutHistory = checkoutHistory;
            _context = context;
            _environment = environment;
            _checkoutService = checkoutService;
            _statusService = statusService;
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
        // checkoutPage and changePage pick the page of each Item History table
        public async Task<IActionResult> Details(int? id, int checkoutPage = 1, int changePage = 1)
        {
            var equipment = id == null
                ? null
                : await _context.Equipment
                    .Include(e => e.Category)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EquipmentId == id);

            // Shows "Item not found." when the item does not exist (FR-20)
            if (equipment == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction(nameof(Index));
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

            // Administrators see who has the item and the Edit, Change Status, and Retire controls
            if (User.IsInRole("Administrator"))
            {
                ViewBag.ActiveCheckout =
                    await _statusService.GetActiveCheckoutAsync(equipment.EquipmentId);

                // Item History (FR-32, US-28): every checkout, plus recorded changes, newest first
                ViewBag.CheckoutHistory =
                    await _checkoutHistory.GetForItemAsync(equipment.EquipmentId, checkoutPage);

                ViewBag.ChangeHistory =
                    await _history.GetChangesAsync(equipment.EquipmentId, changePage);
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
            model.EquipmentId = 0;

            var newCategory = await ValidateFormAsync(model, null, statusLocked: false);

            if (!ModelState.IsValid)
            {
                return await ShowFormAgainAsync(model);
            }

            string? photoPath = null;

            if (model.Photo != null)
            {
                photoPath = await SavePhotoAsync(model.Photo);
            }

            var equipment = new Equipment
            {
                Status = model.Status,
                PhotoPath = photoPath
            };

            CopyFormValues(model, equipment, newCategory);

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

                if (!await AddDuplicateErrorsAsync(model, null, newCategory))
                {
                    throw;
                }

                return await ShowFormAgainAsync(model);
            }

            // Records the new item in its history
            var addedDetails = $"Added with status {equipment.Status}";

            if (equipment.Status == EquipmentOptions.UnderRepairStatus)
            {
                addedDetails += $": {equipment.RepairNote}";
            }

            await _history.AddAsync(
                equipment.EquipmentId,
                EquipmentHistoryService.Added,
                addedDetails,
                GetCurrentUserId());

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Equipment added.";

            return RedirectToAction(
                nameof(Details),
                new { id = equipment.EquipmentId });
        }

        // Opens the Edit Equipment form, filled in with the item's current values (FR-7)
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id)
        {
            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction(nameof(Index));
            }

            var model = new EquipmentFormViewModel
            {
                EquipmentId = equipment.EquipmentId,
                Name = equipment.Name,
                CategoryId = equipment.CategoryId,
                SerialNumber = equipment.SerialNumber,
                Location = equipment.Location,
                Condition = equipment.Condition,
                Status = equipment.Status,
                Description = equipment.Description,
                RepairNote = equipment.RepairNote,
                CurrentPhotoPath = equipment.PhotoPath
            };

            await LoadFormOptionsAsync(model);

            return View("Create", model);
        }

        // Saves changes to an item (FR-8, FR-9)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EquipmentFormViewModel model)
        {
            model.EquipmentId = id;

            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction(nameof(Index));
            }

            model.CurrentPhotoPath = equipment.PhotoPath;

            var oldStatus = equipment.Status;

            // A checked-out or retired item keeps its status on this form
            if (IsStatusLocked(oldStatus))
            {
                model.Status = oldStatus;
            }

            var newCategory = await ValidateFormAsync(model, id, IsStatusLocked(oldStatus));

            if (!ModelState.IsValid)
            {
                return await ShowFormAgainAsync(model);
            }

            // Works out the photo change; old files are deleted only after saving
            var oldPhotoPath = equipment.PhotoPath;
            string? newPhotoPath = null;

            if (model.Photo != null)
            {
                newPhotoPath = await SavePhotoAsync(model.Photo);
                equipment.PhotoPath = newPhotoPath;
            }
            else if (model.RemovePhoto)
            {
                equipment.PhotoPath = null;
            }

            // Notes which details changed, for the item's history
            var changedFields = ListChangedFields(model, equipment, newCategory, oldPhotoPath);

            CopyFormValues(model, equipment, newCategory);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                DeletePhoto(newPhotoPath);

                if (!await AddDuplicateErrorsAsync(model, id, newCategory))
                {
                    throw;
                }

                return await ShowFormAgainAsync(model);
            }

            // Changes the status only if nobody else changed it while the form was open
            if (model.Status != oldStatus)
            {
                var changed = await _context.Equipment
                    .Where(e => e.EquipmentId == id && e.Status == oldStatus)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, model.Status));

                if (changed == 0)
                {
                    await transaction.RollbackAsync();

                    DeletePhoto(newPhotoPath);

                    TempData["ErrorMessage"] =
                        "This item's status was just changed by someone else. Your changes were not saved.";

                    return RedirectToAction(nameof(Edit), new { id });
                }
            }

            // Records the edit and any status change in the item's history
            var adminId = GetCurrentUserId();

            if (changedFields.Count > 0)
            {
                await _history.AddAsync(
                    id,
                    EquipmentHistoryService.Edited,
                    "Changed " + string.Join(", ", changedFields),
                    adminId);
            }

            if (model.Status != oldStatus)
            {
                var statusDetails = $"{oldStatus} to {model.Status}";

                if (model.Status == EquipmentOptions.UnderRepairStatus)
                {
                    statusDetails += $": {equipment.RepairNote}";
                }

                await _history.AddAsync(
                    id,
                    EquipmentHistoryService.StatusChanged,
                    statusDetails,
                    adminId);
            }
            else if (model.Status == EquipmentOptions.UnderRepairStatus &&
                     changedFields.Contains("repair note"))
            {
                await _history.AddAsync(
                    id,
                    EquipmentHistoryService.RepairNoteUpdated,
                    equipment.RepairNote,
                    adminId);
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Removes the old photo file now that the change is saved
            if (oldPhotoPath != equipment.PhotoPath)
            {
                DeletePhoto(oldPhotoPath);
            }

            TempData["SuccessMessage"] = "Changes saved.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // Changes the item's status from its details page (US-10)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, string status, string? repairNote)
        {
            var result = await _statusService.ChangeStatusAsync(
                id,
                status,
                repairNote,
                GetCurrentUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            // Keeps what was typed so it does not have to be entered again
            if (!result.Succeeded)
            {
                TempData["StatusDraft"] = status;
                TempData["RepairNoteDraft"] = repairNote;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // Records a return on the employee's behalf (US-21)
        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordReturn(int id)
        {
            var result = await _checkoutService.RecordReturnAsync(id, GetCurrentUserId() ?? 0);

            if (!result.WasRepeat)
            {
                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // Opens the confirmation page for retiring an item (FR-10)
        [HttpGet]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Retire(int id)
        {
            var equipment = await _context.Equipment
                .Include(e => e.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == id);

            if (equipment == null)
            {
                TempData["ErrorMessage"] = "Item not found.";

                return RedirectToAction(nameof(Index));
            }

            if (equipment.Status == "Retired")
            {
                TempData["ErrorMessage"] = $"{equipment.Name} is already retired.";

                return RedirectToAction(nameof(Details), new { id });
            }

            var activeCheckout = await _statusService.GetActiveCheckoutAsync(id);

            ViewBag.RetireBlockedMessage = activeCheckout == null
                ? null
                : EquipmentStatusService.RetireBlockedMessage(activeCheckout);

            return View(equipment);
        }

        // Retires the item after the administrator confirms (FR-11)
        [HttpPost]
        [ActionName("Retire")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetireConfirmed(int id)
        {
            var result = await _statusService.RetireAsync(id, GetCurrentUserId());

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToAction(nameof(Details), new { id });
        }

        // Checks the Add and Edit form. Returns a new category to create, if one was typed.
        private async Task<Category?> ValidateFormAsync(
            EquipmentFormViewModel model,
            int? editingId,
            bool statusLocked)
        {
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

            // Administrators can set Available, Under Repair, or Lost here.
            // A locked status (the item is Checked Out or Retired) is kept as it is.
            if (!statusLocked &&
                !EquipmentOptions.AdminStatuses.Contains(model.Status))
            {
                ModelState.AddModelError(
                    nameof(model.Status),
                    "Please select a valid status");
            }

            // Serial numbers must be unique, ignoring capitalization (FR-9)
            if (serialNumber.Length > 0 &&
                await SerialNumberInUseAsync(serialNumber, editingId))
            {
                ModelState.AddModelError(
                    nameof(model.SerialNumber),
                    "This serial number is already assigned to another item");
            }

            // Under Repair needs a note explaining why (up to 255 characters)
            if (model.Status == EquipmentOptions.UnderRepairStatus)
            {
                var note = model.RepairNote?.Trim() ?? string.Empty;

                if (note.Length == 0)
                {
                    ModelState.AddModelError(
                        nameof(model.RepairNote),
                        EquipmentOptions.RepairNoteRequiredMessage);
                }
                else if (note.Length > EquipmentOptions.RepairNoteMaxLength)
                {
                    ModelState.AddModelError(
                        nameof(model.RepairNote),
                        EquipmentOptions.RepairNoteTooLongMessage);
                }
            }

            // Checks the photo type and size
            if (model.Photo != null && !await IsValidPhotoAsync(model.Photo))
            {
                ModelState.AddModelError(
                    nameof(model.Photo),
                    PhotoErrorMessage);
            }

            return newCategory;
        }

        // Copies the form's values onto the item (the status is handled separately)
        private static void CopyFormValues(
            EquipmentFormViewModel model,
            Equipment equipment,
            Category? newCategory)
        {
            equipment.Name = model.Name.Trim();
            equipment.SerialNumber = model.SerialNumber.Trim();
            equipment.Location = string.IsNullOrWhiteSpace(model.Location)
                ? null
                : model.Location.Trim();
            equipment.Condition = string.IsNullOrWhiteSpace(model.Condition)
                ? null
                : model.Condition;
            equipment.Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim();

            // The repair note is kept only while the item is Under Repair
            equipment.RepairNote = model.Status == EquipmentOptions.UnderRepairStatus
                ? model.RepairNote?.Trim()
                : null;

            // Saves a new category together with the item, or uses the chosen one
            if (newCategory != null)
            {
                equipment.Category = newCategory;
            }
            else
            {
                equipment.CategoryId = model.CategoryId!.Value;
            }
        }

        // Adds errors when another administrator used the same serial number or
        // category name at the same moment. Returns false if neither was the cause.
        private async Task<bool> AddDuplicateErrorsAsync(
            EquipmentFormViewModel model,
            int? editingId,
            Category? newCategory)
        {
            var serialNumber = model.SerialNumber.Trim();

            var serialTaken = await SerialNumberInUseAsync(serialNumber, editingId);

            var categoryTaken = newCategory != null &&
                await CategoryExistsAsync(newCategory.CategoryName);

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

            return serialTaken || categoryTaken;
        }

        // Shows the Add or Edit form again with its error messages
        private async Task<IActionResult> ShowFormAgainAsync(EquipmentFormViewModel model)
        {
            // Browsers cannot refill a file box, so the user is asked to pick it again
            ViewBag.ReselectPhoto = model.Photo != null &&
                !ModelState.ContainsKey(nameof(model.Photo));

            await LoadFormOptionsAsync(model);

            return View("Create", model);
        }

        // Checks whether another item already has this serial number, ignoring capitalization
        private Task<bool> SerialNumberInUseAsync(string serialNumber, int? editingId)
        {
            var lowerSerial = serialNumber.ToLower();
            var excludeId = editingId ?? 0;

            return _context.Equipment.AnyAsync(e =>
                e.SerialNumber.ToLower() == lowerSerial &&
                e.EquipmentId != excludeId);
        }

        // Lists the details an edit changes, such as "name, location"
        private static List<string> ListChangedFields(
            EquipmentFormViewModel model,
            Equipment equipment,
            Category? newCategory,
            string? oldPhotoPath)
        {
            static string Clean(string? value) => value?.Trim() ?? string.Empty;

            var changed = new List<string>();

            if (Clean(model.Name) != equipment.Name) changed.Add("name");
            if (newCategory != null || model.CategoryId != equipment.CategoryId) changed.Add("category");
            if (Clean(model.SerialNumber) != equipment.SerialNumber) changed.Add("serial number");
            if (Clean(model.Location) != Clean(equipment.Location)) changed.Add("location");
            if (Clean(model.Condition) != Clean(equipment.Condition)) changed.Add("condition");
            if (Clean(model.Description) != Clean(equipment.Description)) changed.Add("description");
            if (equipment.PhotoPath != oldPhotoPath) changed.Add("photo");

            // Only counted while the item stays Under Repair; a status change is recorded separately
            if (model.Status == EquipmentOptions.UnderRepairStatus &&
                equipment.Status == EquipmentOptions.UnderRepairStatus &&
                Clean(model.RepairNote) != Clean(equipment.RepairNote))
            {
                changed.Add("repair note");
            }

            return changed;
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
        }

        // Checked Out and Retired cannot be changed on the Add or Edit form
        private static bool IsStatusLocked(string status)
        {
            return status == "Checked Out" || status == "Retired";
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

            // A checked-out or retired item shows its status without other choices
            model.StatusLocked = IsStatusLocked(model.Status);

            model.Statuses = new SelectList(
                model.StatusLocked ? new[] { model.Status } : EquipmentOptions.AdminStatuses,
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
