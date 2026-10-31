
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 12)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage =
            "The new passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
