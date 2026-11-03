
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Controllers
{
    // Lets administrators add, rename, and delete equipment categories (FR-16 to FR-18)
    [Authorize(Roles = "Administrator")]
    public class CategoriesController : Controller
    {
        private const string DuplicateMessage = "Category already exists.";

        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Shows every category and how many items use it
        public async Task<IActionResult> Index()
        {
            var model = new CategoryIndexViewModel
            {
                Categories = await LoadCategoryListAsync()
            };

            return View(model);
        }

        // Adds a new category from the Categories page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryIndexViewModel model)
        {
            var name = model.NewCategoryName?.Trim() ?? string.Empty;

            var error = await CheckNameAsync(name, null);

            if (error != null)
            {
                ModelState.AddModelError(nameof(model.NewCategoryName), error);

                model.Categories = await LoadCategoryListAsync();

                return View("Index", model);
            }

            _context.Categories.Add(new Category { CategoryName = name });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category \"{name}\" added.";

            return RedirectToAction(nameof(Index));
        }

        // Opens the Rename Category page
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                TempData["ErrorMessage"] = "That category no longer exists.";

                return RedirectToAction(nameof(Index));
            }

            return View(new CategoryEditViewModel
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName
            });
        }

        // Saves a new name for a category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CategoryEditViewModel model)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId);

            if (category == null)
            {
                TempData["ErrorMessage"] = "That category no longer exists.";

                return RedirectToAction(nameof(Index));
            }

            var name = model.CategoryName?.Trim() ?? string.Empty;

            if (ModelState.IsValid)
            {
                var error = await CheckNameAsync(name, category.CategoryId);

                if (error != null)
                {
                    ModelState.AddModelError(nameof(model.CategoryName), error);
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var oldName = category.CategoryName;

            category.CategoryName = name;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category \"{oldName}\" renamed to \"{name}\".";

            return RedirectToAction(nameof(Index));
        }

        // Opens the Delete Category page, listing the items that must be moved first
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                TempData["ErrorMessage"] = "That category no longer exists.";

                return RedirectToAction(nameof(Index));
            }

            var model = new CategoryDeleteViewModel
            {
                CategoryId = category.CategoryId
            };

            await LoadDeletePageAsync(model, category, new Dictionary<int, int?>());

            return View(model);
        }

        // Moves the items to their new categories, then deletes the category
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(CategoryDeleteViewModel model)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId);

            if (category == null)
            {
                TempData["ErrorMessage"] = "That category no longer exists.";

                return RedirectToAction(nameof(Index));
            }

            // The new category chosen for each item, by equipment ID
            var choices = model.Items
                .GroupBy(i => i.EquipmentId)
                .ToDictionary(g => g.Key, g => g.First().NewCategoryId);

            var otherCategoryIds = await _context.Categories
                .Where(c => c.CategoryId != category.CategoryId)
                .Select(c => c.CategoryId)
                .ToListAsync();

            // Uses the items in the database now, in case one was added after the page opened
            var items = await _context.Equipment
                .Where(e => e.CategoryId == category.CategoryId)
                .ToListAsync();

            var allItemsHaveACategory = true;

            foreach (var item in items)
            {
                if (choices.TryGetValue(item.EquipmentId, out var newCategoryId) &&
                    newCategoryId.HasValue &&
                    otherCategoryIds.Contains(newCategoryId.Value))
                {
                    item.CategoryId = newCategoryId.Value;
                }
                else
                {
                    allItemsHaveACategory = false;
                }
            }

            if (!allItemsHaveACategory)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Choose a new category for every item before deleting this category.");

                ViewBag.ShowMissing = true;

                await LoadDeletePageAsync(model, category, choices);

                return View("Delete", model);
            }

            // Saving once moves the items and deletes the category together
            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = items.Count == 0
                ? $"Category \"{category.CategoryName}\" deleted."
                : $"Category \"{category.CategoryName}\" deleted. {items.Count} item(s) were moved.";

            return RedirectToAction(nameof(Index));
        }

        // Returns an error message if the name is blank, too long, or already used
        private async Task<string?> CheckNameAsync(string name, int? currentCategoryId)
        {
            if (name.Length == 0)
            {
                return "Please enter a category name";
            }

            if (name.Length > 100)
            {
                return "Category name must be 100 characters or fewer";
            }

            // Compares names without caring about capitalization (FR-17)
            var lowerName = name.ToLower();

            var exists = await _context.Categories.AnyAsync(c =>
                c.CategoryId != currentCategoryId &&
                c.CategoryName.ToLower() == lowerName);

            return exists ? DuplicateMessage : null;
        }

        // Gets every category with the number of items in it
        private async Task<List<CategoryListItemViewModel>> LoadCategoryListAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryListItemViewModel
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    ItemCount = c.Equipment.Count()
                })
                .ToListAsync();
        }

        // Fills the Delete Category page with the items and the other categories
        private async Task LoadDeletePageAsync(
            CategoryDeleteViewModel model,
            Category category,
            Dictionary<int, int?> choices)
        {
            model.CategoryName = category.CategoryName;

            model.Items = (await _context.Equipment
                    .AsNoTracking()
                    .Where(e => e.CategoryId == category.CategoryId)
                    .OrderBy(e => e.Name)
                    .ToListAsync())
                .Select(e => new CategoryReassignItemViewModel
                {
                    EquipmentId = e.EquipmentId,
                    Name = e.Name,
                    SerialNumber = e.SerialNumber,
                    Status = e.Status,
                    NewCategoryId = choices.TryGetValue(e.EquipmentId, out var chosen)
                        ? chosen
                        : null
                })
                .ToList();

            model.OtherCategories = (await _context.Categories
                    .AsNoTracking()
                    .Where(c => c.CategoryId != category.CategoryId)
                    .OrderBy(c => c.CategoryName)
                    .ToListAsync())
                .Select(c => new SelectListItem(
                    c.CategoryName,
                    c.CategoryId.ToString()))
                .ToList();
        }
    }
}
