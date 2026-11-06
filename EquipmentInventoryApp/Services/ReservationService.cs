
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // One Pending request on the Reservations page (FR-26)
    public class PendingReservationRow
    {
        public int ReservationId { get; set; }

        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // True when approving would overlap an Approved reservation (Approve is disabled)
        public bool OverlapsApproved { get; set; }

        public bool ItemRetired { get; set; }
    }

    // Reservation approval rules (US-24, FR-26, FR-27, FR-36, Milestone 3 Reservations)
    public class ReservationService
    {
        public const int PageSize = 10;

        public const string PendingStatus = "Pending";
        public const string ApprovedStatus = "Approved";
        public const string DeniedStatus = "Denied";

        // The message from the design (FR-27)
        public const string OverlapMessage = "This request overlaps an existing reservation.";

        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly EquipmentHistoryService _history;

        public ReservationService(
            ApplicationDbContext context,
            NotificationService notifications,
            EquipmentHistoryService history)
        {
            _context = context;
            _notifications = notifications;
            _history = history;
        }

        // Pending requests, soonest start date first, one page at a time
        public async Task<PagedResult<PendingReservationRow>> GetPendingAsync(int page)
        {
            var pending = _context.Reservations.Where(r => r.Status == PendingStatus);

            var totalCount = await pending.CountAsync(r => true);

            page = PagedResult<PendingReservationRow>.ClampPage(page, totalCount, PageSize);

            var records = await pending
                .Include(r => r.Equipment)
                .Include(r => r.User)
                .AsNoTracking()
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.ReservationId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Approved reservations for these items, used to disable Approve on overlaps
            var equipmentIds = records.Select(r => r.EquipmentId).Distinct().ToList();

            var approved = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.Status == ApprovedStatus && equipmentIds.Contains(r.EquipmentId))
                .ToListAsync();

            return new PagedResult<PendingReservationRow>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Items = records
                    .Select(r => new PendingReservationRow
                    {
                        ReservationId = r.ReservationId,
                        EquipmentId = r.EquipmentId,
                        ItemName = r.Equipment.Name,
                        SerialNumber = r.Equipment.SerialNumber,
                        EmployeeName = $"{r.User.FirstName} {r.User.LastName}",
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        ItemRetired = r.Equipment.Status == "Retired",
                        OverlapsApproved = approved.Any(a =>
                            a.EquipmentId == r.EquipmentId &&
                            Overlaps(a.StartDate, a.EndDate, r.StartDate, r.EndDate))
                    })
                    .ToList()
            };
        }

        // Approves a Pending request unless it overlaps an Approved reservation for the same item.
        // The employee's notification is saved in the same transaction (Milestone 3).
        public async Task<StatusChangeResult> ApproveAsync(int reservationId, int administratorId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var reservation = await _context.Reservations
                .Include(r => r.Equipment)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation == null || reservation.Status != PendingStatus)
            {
                return Fail("This request has already been handled.");
            }

            // Locks the item's row until the transaction ends, so two administrators cannot
            // approve overlapping requests for the same item at the same moment.
            // The status is set to itself, so nothing about the item changes.
            await _context.Equipment
                .Where(e => e.EquipmentId == reservation.EquipmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, e => e.Status));

            if (reservation.Equipment.Status == "Retired")
            {
                return Fail("This item has been retired, so the request cannot be approved.");
            }

            // Two date ranges overlap when each starts on or before the day the other ends (FR-27)
            var overlaps = await _context.Reservations.AnyAsync(r =>
                r.EquipmentId == reservation.EquipmentId &&
                r.ReservationId != reservation.ReservationId &&
                r.Status == ApprovedStatus &&
                r.StartDate <= reservation.EndDate &&
                reservation.StartDate <= r.EndDate);

            if (overlaps)
            {
                return Fail(OverlapMessage);
            }

            var result = await SaveDecisionAsync(reservation, ApprovedStatus, administratorId);

            if (result == null)
            {
                await transaction.RollbackAsync();

                return Fail("This request has already been handled.");
            }

            await transaction.CommitAsync();

            return result;
        }

        // Denies a Pending request and notifies the employee
        public async Task<StatusChangeResult> DenyAsync(int reservationId, int administratorId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var reservation = await _context.Reservations
                .Include(r => r.Equipment)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation == null || reservation.Status != PendingStatus)
            {
                return Fail("This request has already been handled.");
            }

            var result = await SaveDecisionAsync(reservation, DeniedStatus, administratorId);

            if (result == null)
            {
                await transaction.RollbackAsync();

                return Fail("This request has already been handled.");
            }

            await transaction.CommitAsync();

            return result;
        }

        // Saves the decision, the employee's notification (FR-36), and the item history.
        // The caller commits the transaction. Returns null if the request was no longer Pending.
        private async Task<StatusChangeResult?> SaveDecisionAsync(
            Models.Reservation reservation,
            string newStatus,
            int administratorId)
        {
            // Changes the status only if the request is still Pending
            var changed = await _context.Reservations
                .Where(r => r.ReservationId == reservation.ReservationId && r.Status == PendingStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, newStatus));

            if (changed == 0)
            {
                return null;
            }

            var dates = $"{reservation.StartDate:MM/dd/yyyy} to {reservation.EndDate:MM/dd/yyyy}";
            var decision = newStatus == ApprovedStatus ? "approved" : "denied";

            _notifications.Add(
                reservation.UserId,
                $"Your reservation for {reservation.Equipment.Name} from {dates} was {decision}.");

            var employee = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == reservation.UserId);

            var employeeName = employee == null ? "an employee" : $"{employee.FirstName} {employee.LastName}";

            await _history.AddAsync(
                reservation.EquipmentId,
                newStatus == ApprovedStatus ? "Reservation Approved" : "Reservation Denied",
                $"{employeeName}, {dates}",
                administratorId);

            await _context.SaveChangesAsync();

            return Ok($"Reservation for {reservation.Equipment.Name} ({employeeName}, {dates}) {decision}.");
        }

        private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        {
            return aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;
        }

        private static StatusChangeResult Ok(string message)
        {
            return new StatusChangeResult { Succeeded = true, Message = message };
        }

        private static StatusChangeResult Fail(string message)
        {
            return new StatusChangeResult { Succeeded = false, Message = message };
        }
    }
}
