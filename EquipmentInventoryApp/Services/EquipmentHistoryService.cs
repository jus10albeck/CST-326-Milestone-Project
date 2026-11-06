
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.ViewModels;
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

    // Records and reads each item's change history (status changes, edits, retirement)
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

        // The item's recorded changes (status changes, edits, retirement), newest first,
        // one page at a time. Checkouts are shown separately by CheckoutHistoryService.
        public async Task<PagedResult<ItemHistoryRow>> GetChangesAsync(int equipmentId, int page)
        {
            const int pageSize = 10;

            var events = _context.EquipmentHistory
                .AsNoTracking()
                .Where(h => h.EquipmentId == equipmentId);

            var totalCount = await events.CountAsync(h => true);

            page = PagedResult<ItemHistoryRow>.ClampPage(page, totalCount, pageSize);

            var rows = await events
                .OrderByDescending(h => h.Date)
                .ThenByDescending(h => h.EquipmentHistoryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ItemHistoryRow>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = rows
                    .Select(h => new ItemHistoryRow
                    {
                        Date = h.Date,
                        Event = h.EventType,
                        Details = h.Details,
                        By = h.PerformedByName
                    })
                    .ToList()
            };
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
