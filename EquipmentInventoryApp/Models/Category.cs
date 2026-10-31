
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public ICollection<Equipment> Equipment { get; set; }
            = new List<Equipment>();
    }
}
