
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EquipmentInventoryApp.ViewModels
{
    // Report Damage form (US-29, field Dmg-1)
    public class DamageReportFormViewModel
    {
        public int EquipmentId { get; set; }

        [ValidateNever]
        public string ItemName { get; set; } = string.Empty;

        [ValidateNever]
        public string SerialNumber { get; set; } = string.Empty;

        // Required, 10 to 500 characters (field specification Dmg-1)
        [Required(ErrorMessage = "Please describe the problem (10–500 characters)")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Please describe the problem (10–500 characters)")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;
    }
}
