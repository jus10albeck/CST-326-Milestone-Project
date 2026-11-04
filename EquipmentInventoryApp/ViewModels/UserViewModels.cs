
using System.ComponentModel.DataAnnotations;
using EquipmentInventoryApp.Models;

namespace EquipmentInventoryApp.ViewModels
{
    // One row on the User Management page
    public class UserListItemViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool MustChangePassword { get; set; }

        public bool IsCurrentUser { get; set; }
    }

    // Create Account form (FR-1, fields Acct-1 to Acct-5)
    public class CreateAccountViewModel
    {
        [Required(ErrorMessage = "Please enter a first name")]
        [StringLength(50, ErrorMessage = "First name must be 50 characters or fewer")]
        [RegularExpression(AccountRules.NamePattern, ErrorMessage = "First name can only contain letters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a last name")]
        [StringLength(50, ErrorMessage = "Last name must be 50 characters or fewer")]
        [RegularExpression(AccountRules.NamePattern, ErrorMessage = "Last name can only contain letters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an email")]
        [EmailAddress(ErrorMessage = "Please enter a valid email")]
        [StringLength(255, ErrorMessage = "Email must be 255 characters or fewer")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a role")]
        [Display(Name = "Role")]
        public string Role { get; set; } = "Employee";

        [Required(ErrorMessage = "Please enter a temporary password")]
        [StringLength(100, ErrorMessage = "Password must be 100 characters or fewer")]
        [RegularExpression(AccountRules.PasswordPattern, ErrorMessage = AccountRules.PasswordMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    // Deactivate page shown when the user still has items checked out
    public class DeactivateUserViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public List<MyEquipmentItemViewModel> Items { get; set; }
            = new List<MyEquipmentItemViewModel>();
    }

    // The choice made for one item on the Deactivate page
    public class DeactivateItemChoiceViewModel
    {
        public int EquipmentId { get; set; }

        // True to mark the item Lost; false to return it
        public bool MarkLost { get; set; }
    }

    // Reset Password form (US-4)
    public class ResetPasswordViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a temporary password")]
        [StringLength(100, ErrorMessage = "Password must be 100 characters or fewer")]
        [RegularExpression(AccountRules.PasswordPattern, ErrorMessage = AccountRules.PasswordMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Temporary Password")]
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
