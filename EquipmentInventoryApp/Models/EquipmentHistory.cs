
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    // One event in an item's history, such as a status change or retirement (FR-32).
    // Checkouts and returns come from the Checkouts table instead.
    public class EquipmentHistory
    {
        public int EquipmentHistoryId { get; set; }

        public int EquipmentId { get; set; }

        public DateTime Date { get; set; }

        // Added, Edited, Status Changed, Repair Note Updated, Retired, or Marked Lost
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Details { get; set; }

        // The administrator who made the change. The name is saved too,
        // so it still shows correctly if the account changes later.
        public int? PerformedByUserId { get; set; }

        [MaxLength(201)]
        public string? PerformedByName { get; set; }
    }
}
