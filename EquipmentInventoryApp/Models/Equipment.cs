
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class Equipment
    {
        public int EquipmentId { get; set; }

        public int CategoryId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Location { get; set; }

        [MaxLength(50)]
        public string? Condition { get; set; }

        [Required]
        public string Status { get; set; } = "Available";

        [MaxLength(1000)]
        public string? Description { get; set; }

        public string? PhotoPath { get; set; }

        // Why the item is Under Repair. Required when an administrator sets that status,
        // and cleared when the status changes to something else.
        [MaxLength(255)]
        public string? RepairNote { get; set; }

        public Category Category { get; set; } = null!;

        public ICollection<Checkout> Checkouts { get; set; }
            = new List<Checkout>();

        public ICollection<Reservation> Reservations { get; set; }
            = new List<Reservation>();

        public ICollection<DamageReport> DamageReports { get; set; }
            = new List<DamageReport>();
    }
}
