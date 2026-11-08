
namespace EquipmentInventoryApp.ViewModels
{
    // Request Reservation form on the details page (US-23, fields Res-1 and Res-2).
    // The dates are checked by ReservationService, which shows
    // "Please enter a valid date range" when they are missing or out of order.
    public class ReservationRequestViewModel
    {
        public int EquipmentId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
