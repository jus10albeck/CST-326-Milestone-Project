
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentInventoryApp.ViewModels
{
    // Holds the values entered on the Add Equipment form.
    // Lengths and defaults follow the Milestone 2 field specifications (Equip-1 to Equip-8).
    public class EquipmentFormViewModel
    {
        // Value of the "+ Add New" choice in the Category dropdown
        public const int NewCategoryOption = -1;

        [Required(ErrorMessage = "Please enter a name")]
        [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a category")]
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        // Used only when "+ Add New" is chosen. The controller checks this field
        // so it is not validated by the browser's validation scripts.
        [Display(Name = "New Category Name")]
        public string? NewCategoryName { get; set; }

        [Required(ErrorMessage = "Please enter a serial number")]
        [StringLength(50, ErrorMessage = "Serial Number must be 50 characters or fewer")]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Location must be 100 characters or fewer")]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Display(Name = "Condition")]
        public string? Condition { get; set; } = "Good";

        [Required(ErrorMessage = "Please select a status")]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Available";

        [StringLength(500, ErrorMessage = "Description must be 500 characters or fewer")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Photo")]
        public IFormFile? Photo { get; set; }

        // Dropdown choices. These are filled in by the controller, not by the user.
        [ValidateNever]
        public IEnumerable<SelectListItem> Categories { get; set; }
            = Enumerable.Empty<SelectListItem>();

        [ValidateNever]
        public IEnumerable<SelectListItem> Conditions { get; set; }
            = Enumerable.Empty<SelectListItem>();

        [ValidateNever]
        public IEnumerable<SelectListItem> Statuses { get; set; }
            = Enumerable.Empty<SelectListItem>();
    }
}
