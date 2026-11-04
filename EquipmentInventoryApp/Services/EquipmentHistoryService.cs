
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // One row in the Item History table on the details page
    public class ItemHistoryRow
    {
        public DateTime Date { get; set; }

        public string Event { get; set; } = string.Empty;

        public string? Details { get; set; }

        public string? By { get; set; }
    }

    // Records and reads each item's history (FR-32)
    public class EquipmentHistoryService
    {
        public const string Added = "Added";
        public const string Edited = "Edited";
        public const string StatusChanged = "Status Changed";
        public const string RepairNoteUpdated = "Repair Note Updated";
        public const string Retired = "Retired";
        public const string MarkedLost = "Marked Lost";

        private readonly ApplicationDbContext _context;

        public EquipmentHistoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Adds an event. It is saved the next time SaveChanges runs,
        // so it saves together with the change it describes.
        public async Task AddAsync(
            int equipmentId,
            string eventType,
            string? details,
            int? performedByUserId)
        {
            string? performedByName = null;

            if (performedByUserId.HasValue)
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == performedByUserId.Value);

                performedByName = user == null ? null : $"{user.FirstName} {user.LastName}";
            }

            _context.EquipmentHistory.Add(new EquipmentHistory
            {
                EquipmentId = equipmentId,
                Date = DateTime.Now,
                EventType = eventType,
                Details = Shorten(details, 500),
                PerformedByUserId = performedByUserId,
                PerformedByName = performedByName
            });
        }

        // Builds the item's full history, newest first: recorded events plus
        // every checkout and return from the Checkouts table
        public async Task<List<ItemHistoryRow>> GetHistoryAsync(int equipmentId)
        {
            var rows = new List<ItemHistoryRow>();

            var events = await _context.EquipmentHistory
                .AsNoTracking()
                .Where(h => h.EquipmentId == equipmentId)
                .ToListAsync();

            rows.AddRange(events.Select(h => new ItemHistoryRow
            {
                Date = h.Date,
                Event = h.EventType,
                Details = h.Details,
                By = h.PerformedByName
            }));

            var checkouts = await _context.Checkouts
                .Include(c => c.User)
                .AsNoTracking()
                .Where(c => c.EquipmentId == equipmentId)
                .ToListAsync();

            foreach (var checkout in checkouts)
            {
                var employee = $"{checkout.User.FirstName} {checkout.User.LastName}";

                rows.Add(new ItemHistoryRow
                {
                    Date = checkout.CheckoutDate,
                    Event = "Checked Out",
                    Details = $"Due {checkout.DueDate:MM/dd/yyyy}",
                    By = employee
                });

                // Lost items are recorded as their own event when they are marked Lost
                if (checkout.ReturnDate.HasValue && !checkout.MarkedLost)
                {
                    var daysLate = (checkout.ReturnDate.Value.Date - checkout.DueDate.Date).Days;

                    rows.Add(new ItemHistoryRow
                    {
                        Date = checkout.ReturnDate.Value,
                        Event = "Returned",
                        Details = daysLate > 0 ? $"{daysLate} day(s) late" : "On time",
                        By = employee
                    });
                }
            }

            return rows
                .OrderByDescending(r => r.Date)
                .ToList();
        }

        private static string? Shorten(string? text, int maxLength)
        {
            if (text == null || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}
