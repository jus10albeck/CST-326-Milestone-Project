
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class Reservation
    {
        public int ReservationId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int EquipmentId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending";

        public User User { get; set; } = null!;

        public Equipment Equipment { get; set; } = null!;
    }
}
