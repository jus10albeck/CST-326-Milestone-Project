
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

        // Statuses an administrator can set by hand. Checked Out and Reserved are set by
        // the system, and Retired has its own button with a confirmation step.
        public static readonly string[] AdminStatuses =
        {
            "Available",
            "Under Repair",
            "Lost"
        };

        // Under Repair needs a note explaining why (up to 255 characters)
        public const string UnderRepairStatus = "Under Repair";

        public const int RepairNoteMaxLength = 255;

        public const string RepairNoteRequiredMessage =
            "Please enter a note explaining why the item is under repair";

        public const string RepairNoteTooLongMessage =
            "Repair note must be 255 characters or fewer";

        public static readonly string[] Conditions =
        {
            "New",
            "Good",
            "Fair",
            "Poor"
        };
    }
}
