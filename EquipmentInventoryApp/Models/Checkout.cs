
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class Checkout
    {
        public int CheckoutId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int EquipmentId { get; set; }

        [Required]
        public DateTime CheckoutDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        public DateTime? ReturnDate { get; set; }

        public User User { get; set; } = null!;

        public Equipment Equipment { get; set; } = null!;
    }
}
