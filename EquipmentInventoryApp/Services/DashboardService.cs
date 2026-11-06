
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // Dashboard counts (FR-39) and the Overdue List (FR-41). Everything is queried
    // from the database each time it is asked for; nothing is stored or cached.
    public class DashboardService
    {
        public const int OverdueListPageSize = 10;

        // How many overdue items the dashboard shows before linking to the full list
        public const int DashboardOverdueRows = 5;

        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Checkouts that have not been returned and are past their due date. This one
        // calculation is used for the dashboard count and the Overdue List (Milestone 3).
        public IQueryable<Checkout> OverdueCheckouts()
        {
            var today = DateTime.Today;

            return _context.Checkouts
                .Where(c => c.ReturnDate == null && c.DueDate < today);
        }

        // Every dashboard count, queried fresh
        public async Task<AdminDashboardViewModel> GetDashboardAsync()
        {
            var model = new AdminDashboardViewModel
            {
                TotalEquipmentCount = await _context.Equipment
                    .CountAsync(e => true),

                CheckedOutCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Checked Out"),

                OverdueCount = await OverdueCheckouts()
                    .CountAsync(c => true),

                UnderRepairCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Under Repair"),

                LostCount = await _context.Equipment
                    .CountAsync(e => e.Status == "Lost")
            };

            model.OverdueItems = await GetOverdueRowsAsync(0, DashboardOverdueRows);

            return model;
        }

        // The Overdue List, most overdue first, one page at a time (FR-41)
        public async Task<PagedResult<OverdueItemViewModel>> GetOverdueListAsync(int page)
        {
            var totalCount = await OverdueCheckouts().CountAsync(c => true);

            page = PagedResult<OverdueItemViewModel>.ClampPage(page, totalCount, OverdueListPageSize);

            return new PagedResult<OverdueItemViewModel>
            {
                Page = page,
                PageSize = OverdueListPageSize,
                TotalCount = totalCount,
                Items = await GetOverdueRowsAsync((page - 1) * OverdueListPageSize, OverdueListPageSize)
            };
        }

        private async Task<List<OverdueItemViewModel>> GetOverdueRowsAsync(int skip, int take)
        {
            var today = DateTime.Today;

            // The earliest due date is the most overdue
            var checkouts = await OverdueCheckouts()
                .Include(c => c.Equipment)
                .Include(c => c.User)
                .AsNoTracking()
                .OrderBy(c => c.DueDate)
                .ThenBy(c => c.CheckoutId)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return checkouts
                .Select(c => new OverdueItemViewModel
                {
                    EquipmentId = c.EquipmentId,
                    ItemName = c.Equipment.Name,
                    SerialNumber = c.Equipment.SerialNumber,
                    EmployeeName = $"{c.User.FirstName} {c.User.LastName}",
                    DueDate = c.DueDate,
                    DaysOverdue = (today - c.DueDate.Date).Days
                })
                .ToList();
        }
    }
}
