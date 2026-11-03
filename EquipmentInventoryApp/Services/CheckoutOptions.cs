
namespace EquipmentInventoryApp.Services
{
    // Checkout settings. These can be changed in appsettings.json under "Checkout".
    // The values below are used when no setting is given.
    public class CheckoutOptions
    {
        // Number of days from checkout to the due date (Loan Period)
        public int LoanPeriodDays { get; set; } = 7;

        // Most items one employee can have checked out at the same time (US-22)
        public int MaxActiveCheckouts { get; set; } = 3;
    }
}
