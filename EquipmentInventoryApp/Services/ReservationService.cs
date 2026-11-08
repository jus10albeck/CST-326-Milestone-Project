
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

    // One of the employee's own requests on My Reservations (FR-29)
    public class MyReservationRow
    {
        public int ReservationId { get; set; }

        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Pending, Approved, Denied, or Cancelled
        public string Status { get; set; } = string.Empty;

        // Pending or Approved requests can be cancelled until their end date has passed
        public bool CanCancel { get; set; }
    }

    // One Approved reservation that has not ended yet, on the administrator's Reservations page
    public class ApprovedReservationRow
    {
        public int ReservationId { get; set; }

        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string EmployeeName { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        // Today falls inside the date range, so the item shows as Reserved now
        public bool InProgress { get; set; }
    }

    // An Approved date range shown on the details page so employees can pick other dates
    public class ReservedDateRange
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }

    // Reservation approval rules (US-24, FR-26, FR-27, FR-36, Milestone 3 Reservations)
    public class ReservationService
    {
        public const int PageSize = 10;

        public const string PendingStatus = "Pending";
        public const string ApprovedStatus = "Approved";
        public const string DeniedStatus = "Denied";
        public const string CancelledStatus = "Cancelled";

        // The message from the design (FR-27)
        public const string OverlapMessage = "This request overlaps an existing reservation.";

        // The date range message from the field specification (Res-1, Res-2)
        public const string DateRangeMessage = "Please enter a valid date range";

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

        // Saves a reservation request as Pending (US-23, Milestone 3 Reservation Design).
        // The start date must be today or later, the end date on or after the start date,
        // and the item must not be Retired.
        public async Task<StatusChangeResult> RequestAsync(
            int equipmentId,
            int userId,
            DateTime? startDate,
            DateTime? endDate)
        {
            var today = DateTime.Today;

            if (startDate == null || endDate == null ||
                startDate.Value.Date < today ||
                endDate.Value.Date < startDate.Value.Date)
            {
                return Fail(DateRangeMessage);
            }

            var start = startDate.Value.Date;
            var end = endDate.Value.Date;

            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return Fail("Item not found.");
            }

            if (equipment.Status == "Retired")
            {
                return Fail("This item has been retired, so it cannot be reserved.");
            }

            // Stops the same employee from sending the same dates twice
            var alreadyRequested = await _context.Reservations.AnyAsync(r =>
                r.EquipmentId == equipmentId &&
                r.UserId == userId &&
                (r.Status == PendingStatus || r.Status == ApprovedStatus) &&
                r.StartDate <= end &&
                start <= r.EndDate);

            if (alreadyRequested)
            {
                return Fail("You already have a request for this item that covers some of these dates. See My Reservations.");
            }

            _context.Reservations.Add(new Models.Reservation
            {
                EquipmentId = equipmentId,
                UserId = userId,
                StartDate = start,
                EndDate = end,
                Status = PendingStatus
            });

            var dates = $"{start:MM/dd/yyyy} to {end:MM/dd/yyyy}";

            await _history.AddAsync(equipmentId, "Reservation Requested", dates, userId);

            await _context.SaveChangesAsync();

            return Ok($"Reservation requested for {equipment.Name} from {dates}. It is Pending until an administrator reviews it.");
        }

        // The employee's own requests, newest request first (FR-29)
        public async Task<PagedResult<MyReservationRow>> GetForUserAsync(int userId, int page)
        {
            var mine = _context.Reservations.Where(r => r.UserId == userId);

            var totalCount = await mine.CountAsync(r => true);

            page = PagedResult<MyReservationRow>.ClampPage(page, totalCount, PageSize);

            var items = await mine
                .AsNoTracking()
                .OrderByDescending(r => r.ReservationId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new MyReservationRow
                {
                    ReservationId = r.ReservationId,
                    EquipmentId = r.EquipmentId,
                    ItemName = r.Equipment.Name,
                    SerialNumber = r.Equipment.SerialNumber,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    Status = r.Status
                })
                .ToListAsync();

            var today = DateTime.Today;

            foreach (var item in items)
            {
                item.CanCancel = IsCancellable(item.Status, item.EndDate, today);
            }

            return new PagedResult<MyReservationRow>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

        // Approved date ranges for an item that have not ended yet, soonest first
        public async Task<List<ReservedDateRange>> GetUpcomingApprovedAsync(int equipmentId)
        {
            var today = DateTime.Today;

            return await _context.Reservations
                .AsNoTracking()
                .Where(r => r.EquipmentId == equipmentId &&
                            r.Status == ApprovedStatus &&
                            r.EndDate >= today)
                .OrderBy(r => r.StartDate)
                .Select(r => new ReservedDateRange { StartDate = r.StartDate, EndDate = r.EndDate })
                .ToListAsync();
        }

        // Approved reservations that have not ended yet, soonest first (administrators)
        public async Task<PagedResult<ApprovedReservationRow>> GetApprovedAsync(int page)
        {
            var today = DateTime.Today;

            var approved = _context.Reservations
                .Where(r => r.Status == ApprovedStatus && r.EndDate >= today);

            var totalCount = await approved.CountAsync(r => true);

            page = PagedResult<ApprovedReservationRow>.ClampPage(page, totalCount, PageSize);

            var items = await approved
                .AsNoTracking()
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.ReservationId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(r => new ApprovedReservationRow
                {
                    ReservationId = r.ReservationId,
                    EquipmentId = r.EquipmentId,
                    ItemName = r.Equipment.Name,
                    SerialNumber = r.Equipment.SerialNumber,
                    EmployeeName = r.User.FirstName + " " + r.User.LastName,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.InProgress = item.StartDate.Date <= today;
            }

            return new PagedResult<ApprovedReservationRow>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

        // An employee cancels one of their own Pending or Approved requests
        public async Task<StatusChangeResult> CancelByEmployeeAsync(int reservationId, int userId)
        {
            return await CancelAsync(reservationId, userId, cancelledByAdministrator: false);
        }

        // An administrator cancels an employee's reservation. The employee is notified.
        public async Task<StatusChangeResult> CancelByAdministratorAsync(int reservationId, int administratorId)
        {
            return await CancelAsync(reservationId, administratorId, cancelledByAdministrator: true);
        }

        // Sets the status to Cancelled. A cancelled reservation no longer makes the item
        // show as Reserved or blocks other requests from being approved.
        private async Task<StatusChangeResult> CancelAsync(
            int reservationId,
            int performedByUserId,
            bool cancelledByAdministrator)
        {
            var today = DateTime.Today;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var reservation = await _context.Reservations
                .Include(r => r.Equipment)
                .Include(r => r.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            // Employees can only cancel their own requests
            if (reservation == null ||
                (!cancelledByAdministrator && reservation.UserId != performedByUserId))
            {
                return Fail("Reservation not found.");
            }

            if (!IsCancellable(reservation.Status, reservation.EndDate, today))
            {
                return Fail("This reservation can no longer be cancelled.");
            }

            // Changes the status only if it has not changed since it was read
            var changed = await _context.Reservations
                .Where(r => r.ReservationId == reservationId &&
                            r.Status == reservation.Status)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, CancelledStatus));

            if (changed == 0)
            {
                await transaction.RollbackAsync();

                return Fail("This reservation can no longer be cancelled.");
            }

            var dates = $"{reservation.StartDate:MM/dd/yyyy} to {reservation.EndDate:MM/dd/yyyy}";
            var employeeName = $"{reservation.User.FirstName} {reservation.User.LastName}";

            if (cancelledByAdministrator)
            {
                _notifications.Add(
                    reservation.UserId,
                    $"Your reservation for {reservation.Equipment.Name} from {dates} was cancelled by an administrator.");
            }

            await _history.AddAsync(
                reservation.EquipmentId,
                "Reservation Cancelled",
                $"{employeeName}, {dates}" + (cancelledByAdministrator ? "" : " (cancelled by the employee)"),
                performedByUserId);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(cancelledByAdministrator
                ? $"Reservation for {reservation.Equipment.Name} ({employeeName}, {dates}) cancelled. The employee has been notified."
                : $"Your reservation for {reservation.Equipment.Name} from {dates} was cancelled.");
        }

        private static bool IsCancellable(string status, DateTime endDate, DateTime today)
        {
            return (status == PendingStatus || status == ApprovedStatus) && endDate.Date >= today;
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
