
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // One checkout in a history table
    public class CheckoutHistoryRow
    {
        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public DateTime CheckoutDate { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? ReturnDate { get; set; }

        public bool MarkedLost { get; set; }

        // The administrator who recorded the return, if the employee did not (US-21)
        public string? RecordedByName { get; set; }

        public bool IsOverdue => ReturnDate == null && DueDate.Date < DateTime.Today;

        // How the checkout ended, or is going
        public string Result
        {
            get
            {
                if (MarkedLost)
                {
                    return "Lost";
                }

                if (ReturnDate == null)
                {
                    return IsOverdue ? "Overdue" : "Checked Out";
                }

                var daysLate = (ReturnDate.Value.Date - DueDate.Date).Days;

                return daysLate > 0 ? $"Returned {daysLate} day(s) late" : "Returned on time";
            }
        }
    }

    // Reads checkout history for one item (FR-32, US-28) or one employee (US-27)
    public class CheckoutHistoryService
    {
        public const int PageSize = 10;

        private readonly ApplicationDbContext _context;

        public CheckoutHistoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Every checkout of an item, newest first, one page at a time
        public Task<PagedResult<CheckoutHistoryRow>> GetForItemAsync(int equipmentId, int page)
        {
            return GetPageAsync(_context.Checkouts.Where(c => c.EquipmentId == equipmentId), page);
        }

        // Every checkout an employee has made, newest first, one page at a time
        public Task<PagedResult<CheckoutHistoryRow>> GetForUserAsync(int userId, int page)
        {
            return GetPageAsync(_context.Checkouts.Where(c => c.UserId == userId), page);
        }

        private async Task<PagedResult<CheckoutHistoryRow>> GetPageAsync(
            IQueryable<Checkout> checkouts,
            int page)
        {
            var totalCount = await checkouts.CountAsync(c => true);

            page = PagedResult<CheckoutHistoryRow>.ClampPage(page, totalCount, PageSize);

            var records = await checkouts
                .Include(c => c.Equipment)
                .Include(c => c.User)
                .AsNoTracking()
                .OrderByDescending(c => c.CheckoutDate)
                .ThenByDescending(c => c.CheckoutId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Looks up the names of administrators who recorded returns
            var recorderIds = records
                .Where(c => c.ReturnRecordedByUserId.HasValue)
                .Select(c => c.ReturnRecordedByUserId!.Value)
                .Distinct()
                .ToList();

            var recorderNames = (await _context.Users
                    .AsNoTracking()
                    .Where(u => recorderIds.Contains(u.UserId))
                    .ToListAsync())
                .ToDictionary(u => u.UserId, u => $"{u.FirstName} {u.LastName}");

            return new PagedResult<CheckoutHistoryRow>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Items = records
                    .Select(c => new CheckoutHistoryRow
                    {
                        EquipmentId = c.EquipmentId,
                        ItemName = c.Equipment.Name,
                        SerialNumber = c.Equipment.SerialNumber,
                        EmployeeName = $"{c.User.FirstName} {c.User.LastName}",
                        CheckoutDate = c.CheckoutDate,
                        DueDate = c.DueDate,
                        ReturnDate = c.ReturnDate,
                        MarkedLost = c.MarkedLost,
                        RecordedByName = c.ReturnRecordedByUserId.HasValue &&
                                         recorderNames.TryGetValue(c.ReturnRecordedByUserId.Value, out var name)
                            ? name
                            : null
                    })
                    .ToList()
            };
        }
    }
}
