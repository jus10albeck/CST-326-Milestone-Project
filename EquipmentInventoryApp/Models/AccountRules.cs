
namespace EquipmentInventoryApp.Models
{
    // Account rules from the Milestone 2 field specifications (Acct-1 to Acct-5)
    public static class AccountRules
    {
        // At least 8 characters, with at least one letter and one number
        public const string PasswordPattern = @"^(?=.*[A-Za-z])(?=.*\d).{8,}$";

        public const string PasswordMessage =
            "Password must be at least 8 characters and include a letter and a number";

        // Letters, plus spaces, hyphens, and apostrophes for names like Mary-Jane or O'Brien
        public const string NamePattern = @"^[A-Za-z][A-Za-z '\-]*$";

        public const string ActiveStatus = "Active";
        public const string DeactivatedStatus = "Deactivated";
        public const string PendingStatus = "Pending";

        public static readonly string[] Roles = { "Employee", "Administrator" };
    }
}
