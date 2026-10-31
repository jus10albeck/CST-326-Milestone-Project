
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Employee";

        [Required]
        public string Status { get; set; } = "Active";

        public bool MustChangePassword { get; set; }

        public ICollection<Checkout> Checkouts { get; set; }
            = new List<Checkout>();

        public ICollection<Reservation> Reservations { get; set; }
            = new List<Reservation>();

        public ICollection<DamageReport> DamageReports { get; set; }
            = new List<DamageReport>();

        public ICollection<Notification> Notifications { get; set; }
            = new List<Notification>();
    }
}
