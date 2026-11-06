
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // Creates and reads on-screen notifications (FR-36, FR-37). Notifications are
    // on-screen only; no email is sent (Milestone 2 scope).
    public class NotificationService
    {
        // The most notifications shown at once in the page banner area
        public const int MaxShown = 5;

        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Adds a notification for one user. It is saved the next time SaveChanges runs,
        // so it saves in the same transaction as the change it describes.
        public void Add(int userId, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message.Length > 1000 ? message.Substring(0, 997) + "..." : message,
                CreatedDate = DateTime.Now,
                IsRead = false
            });
        }

        // Adds the same notification for every active administrator
        public async Task AddForAdministratorsAsync(string message)
        {
            var adminIds = await _context.Users
                .Where(u => u.Role == "Administrator" && u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
            {
                Add(adminId, message);
            }
        }

        // The user's notifications that have not been dismissed, newest first
        public Task<List<Notification>> GetUnreadAsync(int userId)
        {
            return _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.NotificationId)
                .ToListAsync();
        }

        // Marks one of the user's notifications as dismissed
        public Task<int> DismissAsync(int notificationId, int userId)
        {
            return _context.Notifications
                .Where(n => n.NotificationId == notificationId && n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        // Marks all of the user's notifications as dismissed
        public Task<int> DismissAllAsync(int userId)
        {
            return _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }
    }
}
