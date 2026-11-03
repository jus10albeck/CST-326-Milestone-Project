
namespace EquipmentInventoryApp.ViewModels
{
    // My Equipment page: the items the employee has checked out now (FR-30)
    public class MyEquipmentViewModel
    {
        public List<MyEquipmentItemViewModel> Items { get; set; }
            = new List<MyEquipmentItemViewModel>();

        public int MaxActiveCheckouts { get; set; }
    }

    // One checked-out item
    public class MyEquipmentItemViewModel
    {
        public int CheckoutId { get; set; }

        public int EquipmentId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public DateTime CheckoutDate { get; set; }

        public DateTime DueDate { get; set; }

        // Past its due date and not returned (FR-31)
        public bool IsOverdue { get; set; }
    }
}
