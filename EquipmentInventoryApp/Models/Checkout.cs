
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

        // The date the checkout ended. Set when the item is returned or marked Lost.
        public DateTime? ReturnDate { get; set; }

        // True when the checkout ended because the item was marked Lost instead of returned
        public bool MarkedLost { get; set; }

        public User User { get; set; } = null!;

        public Equipment Equipment { get; set; } = null!;
    }
}
