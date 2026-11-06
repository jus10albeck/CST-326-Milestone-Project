
namespace EquipmentInventoryApp.ViewModels
{
    // Holds the numbers and overdue list shown on the administrator dashboard
    public class AdminDashboardViewModel
    {
        public int TotalEquipmentCount { get; set; }

        public int CheckedOutCount { get; set; }

        public int OverdueCount { get; set; }

        public int UnderRepairCount { get; set; }

        public int LostCount { get; set; }

        public List<OverdueItemViewModel> OverdueItems { get; set; }
            = new List<OverdueItemViewModel>();
    }

    // One row in the Overdue Equipment table
    public class OverdueItemViewModel
    {
        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public int DaysOverdue { get; set; }
    }
}
