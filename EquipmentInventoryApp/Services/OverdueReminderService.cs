
using EquipmentInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // One overdue item listed in the reminder banner
    public class OverdueReminderItem
    {
        public int EquipmentId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public int DaysOverdue { get; set; }
    }

    // Overdue reminder shown after an employee logs in (US-31, Milestone 3 Notifications).
    // It is not stored as a notification. It is worked out from the employee's active
    // checkouts that are past due each time it is shown, so it goes away on its own once
    // those items are returned.
    public class OverdueReminderService
    {
        // Set when the employee dismisses the banner. It lasts until the browser closes
        // and is cleared at each login, so the reminder shows again after the next login.
        public const string DismissedCookie = "OverdueReminderDismissed";

        private readonly ApplicationDbContext _context;

        public OverdueReminderService(ApplicationDbContext context)
        {
            _context = context;
        }

        // The employee's checkouts that are past their due date, most overdue first
        public async Task<List<OverdueReminderItem>> GetOverdueAsync(int userId)
        {
            var today = DateTime.Today;

            var checkouts = await _context.Checkouts
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.ReturnDate == null && c.DueDate < today)
                .OrderBy(c => c.DueDate)
                .Select(c => new
                {
                    c.EquipmentId,
                    c.Equipment.Name,
                    c.Equipment.SerialNumber,
                    c.DueDate
                })
                .ToListAsync();

            return checkouts
                .Select(c => new OverdueReminderItem
                {
                    EquipmentId = c.EquipmentId,
                    Name = c.Name,
                    SerialNumber = c.SerialNumber,
                    DueDate = c.DueDate,
                    DaysOverdue = (today - c.DueDate.Date).Days
                })
                .ToList();
        }
    }
}
