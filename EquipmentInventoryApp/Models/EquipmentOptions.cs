
namespace EquipmentInventoryApp.Models
{
    // The fixed choices used by the equipment forms and filters
    public static class EquipmentOptions
    {
        public static readonly string[] Statuses =
        {
            "Available",
            "Checked Out",
            "Reserved",
            "Under Repair",
            "Lost",
            "Retired"
        };

        // The status shown to users. An Available item with an Approved reservation
        // covering today is shown as Reserved (FR-28).
        public static string DisplayStatus(string storedStatus, bool isReservedToday)
        {
            return storedStatus == "Available" && isReservedToday
                ? "Reserved"
                : storedStatus;
        }

        public static readonly string[] Conditions =
        {
            "New",
            "Good",
            "Fair",
            "Poor"
        };
    }
}
