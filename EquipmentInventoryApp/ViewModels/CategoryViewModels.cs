
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentInventoryApp.ViewModels
{
    // Categories page: the list of categories and the Add Category box
    public class CategoryIndexViewModel
    {
        [ValidateNever]
        public List<CategoryListItemViewModel> Categories { get; set; }
            = new List<CategoryListItemViewModel>();

        // Checked by the controller (required, unique, 100 characters or fewer)
        [Display(Name = "New Category")]
        public string? NewCategoryName { get; set; }
    }

    // One row in the categories table
    public class CategoryListItemViewModel
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public int ItemCount { get; set; }
    }

    // Rename Category page
    public class CategoryEditViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please enter a category name")]
        [StringLength(100, ErrorMessage = "Category name must be 100 characters or fewer")]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;
    }

    // Delete Category page: the items that must be moved before deleting
    public class CategoryDeleteViewModel
    {
        public int CategoryId { get; set; }

        [ValidateNever]
        public string CategoryName { get; set; } = string.Empty;

        public List<CategoryReassignItemViewModel> Items { get; set; }
            = new List<CategoryReassignItemViewModel>();

        // The categories the items can be moved to
        [ValidateNever]
        public IEnumerable<SelectListItem> OtherCategories { get; set; }
            = Enumerable.Empty<SelectListItem>();
    }

    // One item on the Delete Category page and the category it will move to
    public class CategoryReassignItemViewModel
    {
        public int EquipmentId { get; set; }

        [ValidateNever]
        public string Name { get; set; } = string.Empty;

        [ValidateNever]
        public string SerialNumber { get; set; } = string.Empty;

        [ValidateNever]
        public string Status { get; set; } = string.Empty;

        public int? NewCategoryId { get; set; }
    }
}
