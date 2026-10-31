
using System.ComponentModel.DataAnnotations;

namespace EquipmentInventoryApp.Models
{
    public class DamageReport
    {
        public int DamageReportId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int EquipmentId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime ReportDate { get; set; }

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Open";

        public DateTime? CloseDate { get; set; }

        public User User { get; set; } = null!;

        public Equipment Equipment { get; set; } = null!;
    }
}
