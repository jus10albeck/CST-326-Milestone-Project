
using EquipmentInventoryApp.Data;

namespace EquipmentInventoryApp.Services
{
    // Reserved is not stored on the item. It is worked out from Approved reservations
    // and today's date each time it is needed (FR-28, Milestone 3 Background Tasks).
    public static class ReservationQueries
    {
        // IDs of items with an Approved reservation whose date range includes the given day
        public static IQueryable<int> ReservedEquipmentIds(
            this ApplicationDbContext context,
            DateTime day)
        {
            return context.Reservations
                .Where(r => r.Status == "Approved" &&
                            r.StartDate <= day &&
                            r.EndDate >= day)
                .Select(r => r.EquipmentId);
        }
    }
}
