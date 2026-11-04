
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EquipmentInventoryApp.Services
{
    // Whether the current employee can check out an item, and why not if they cannot
    public class CheckoutAvailability
    {
        public bool CanCheckOut { get; set; }

        public string? Reason { get; set; }

        // The due date the item would get if it were checked out today
        public DateTime DueDateIfCheckedOutToday { get; set; }

        public int ActiveCheckouts { get; set; }

        public int MaxActiveCheckouts { get; set; }
    }

    // The outcome of a checkout attempt
    public class CheckoutResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? ItemName { get; set; }

        public DateTime? DueDate { get; set; }

        // True when this was a repeat of a request the same person just made,
        // such as a double-click. Nothing was changed and no message is needed.
        public bool WasRepeat { get; set; }
    }

    // Holds the checkout business rules (Milestone 3, Equipment Checkout)
    public class CheckoutService
    {
        private const string AvailableStatus = "Available";
        private const string CheckedOutStatus = "Checked Out";

        private readonly ApplicationDbContext _context;
        private readonly CheckoutOptions _options;
        private readonly EquipmentHistoryService _history;

        public CheckoutService(
            ApplicationDbContext context,
            IOptions<CheckoutOptions> options,
            EquipmentHistoryService history)
        {
            _context = context;
            _options = options.Value;
            _history = history;
        }

        public int LoanPeriodDays => _options.LoanPeriodDays;

        public int MaxActiveCheckouts => _options.MaxActiveCheckouts;

        // Checks whether the employee can check out the item right now.
        // Used to enable or disable the Check Out button on the details page.
        public async Task<CheckoutAvailability> GetAvailabilityAsync(
            Equipment equipment,
            int userId)
        {
            var today = DateTime.Today;
            var activeCheckouts = await CountActiveCheckoutsAsync(userId);

            var availability = new CheckoutAvailability
            {
                DueDateIfCheckedOutToday = today.AddDays(_options.LoanPeriodDays),
                ActiveCheckouts = activeCheckouts,
                MaxActiveCheckouts = _options.MaxActiveCheckouts
            };

            if (equipment.Status != AvailableStatus)
            {
                availability.Reason = NotAvailableMessage(equipment.Status);
            }
            else if (await IsReservedForAnotherUserAsync(equipment.EquipmentId, userId, today))
            {
                availability.Reason = ReservedMessage;
            }
            else if (activeCheckouts >= _options.MaxActiveCheckouts)
            {
                availability.Reason = LimitMessage;
            }
            else
            {
                availability.CanCheckOut = true;
            }

            return availability;
        }

        // Checks the item out to the employee as one database transaction (NFR-9).
        // Either the checkout record and the Checked Out status are both saved, or neither is.
        public async Task<CheckoutResult> CheckOutAsync(int equipmentId, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Locks this employee's row until the transaction ends, so two checkouts by the
            // same employee at the same moment cannot both pass the checkout limit.
            // The value is set to itself, so nothing about the user actually changes.
            await _context.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, u => u.SecurityStamp));

            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return Fail("Item not found.");
            }

            var today = DateTime.Today;

            if (equipment.Status != AvailableStatus)
            {
                // A repeat of a checkout this employee just made (for example, a double-click)
                // is not treated as an error
                var justCheckedOut = DateTime.Now.AddMinutes(-RepeatWindowMinutes);

                if (await _context.Checkouts.AnyAsync(c =>
                    c.EquipmentId == equipmentId &&
                    c.UserId == userId &&
                    c.ReturnDate == null &&
                    c.CheckoutDate >= justCheckedOut))
                {
                    return Repeat();
                }

                return Fail(NotAvailableMessage(equipment.Status));
            }

            if (await IsReservedForAnotherUserAsync(equipmentId, userId, today))
            {
                return Fail(ReservedMessage);
            }

            if (await CountActiveCheckoutsAsync(userId) >= _options.MaxActiveCheckouts)
            {
                return Fail(LimitMessage);
            }

            // Changes the status only if the item is still Available. If another employee
            // checked it out first, no row is changed and this checkout stops here.
            var claimed = await _context.Equipment
                .Where(e => e.EquipmentId == equipmentId && e.Status == AvailableStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, CheckedOutStatus));

            if (claimed == 0)
            {
                await transaction.RollbackAsync();

                return Fail(NotAvailableMessage(CheckedOutStatus));
            }

            var dueDate = today.AddDays(_options.LoanPeriodDays);

            _context.Checkouts.Add(new Checkout
            {
                UserId = userId,
                EquipmentId = equipmentId,
                CheckoutDate = DateTime.Now,
                DueDate = dueDate
            });

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return new CheckoutResult
            {
                Succeeded = true,
                ItemName = equipment.Name,
                DueDate = dueDate,
                Message = $"You checked out {equipment.Name}. It is due {dueDate:MM/dd/yyyy}."
            };
        }

        // Returns an item the employee has checked out (FR-25, Milestone 3 Equipment Return).
        // The checkout record is kept, with its return date, for history.
        public async Task<CheckoutResult> ReturnAsync(int equipmentId, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Finds this employee's active checkout for the item
            var checkout = await _context.Checkouts
                .Include(c => c.Equipment)
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.EquipmentId == equipmentId &&
                    c.UserId == userId &&
                    c.ReturnDate == null);

            if (checkout == null)
            {
                // A repeat of a return this employee just made (for example, a double-click)
                // is not treated as an error. A later attempt still shows the message (ATP-04).
                var justReturned = DateTime.Now.AddMinutes(-RepeatWindowMinutes);

                if (await _context.Checkouts.AnyAsync(c =>
                    c.EquipmentId == equipmentId &&
                    c.UserId == userId &&
                    c.ReturnDate != null &&
                    c.ReturnDate >= justReturned))
                {
                    return Repeat();
                }

                return Fail(NotCheckedOutToYouMessage);
            }

            // Records the return date only if it has not been recorded yet,
            // so clicking Return twice cannot record a second return (ATP-04)
            var returned = await _context.Checkouts
                .Where(c => c.CheckoutId == checkout.CheckoutId && c.ReturnDate == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReturnDate, DateTime.Now));

            if (returned == 0)
            {
                await transaction.RollbackAsync();

                return Fail(NotCheckedOutToYouMessage);
            }

            // Sets the item back to Available. If an administrator has changed it to
            // another status (such as Under Repair) while it was out, that status is kept.
            await _context.Equipment
                .Where(e => e.EquipmentId == equipmentId && e.Status == CheckedOutStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, AvailableStatus));

            await transaction.CommitAsync();

            return new CheckoutResult
            {
                Succeeded = true,
                ItemName = checkout.Equipment.Name,
                Message = $"You returned {checkout.Equipment.Name}."
            };
        }

        // Ends every checkout a user has, such as before their account is deactivated (US-21).
        // Items in lostEquipmentIds are marked Lost; the rest are returned and set to Available.
        // Call inside a transaction so it saves together with the other changes.
        public async Task<(int Returned, int Lost)> CloseAllForUserAsync(
            int userId,
            ISet<int> lostEquipmentIds,
            int? performedByUserId)
        {
            var active = await _context.Checkouts
                .Where(c => c.UserId == userId && c.ReturnDate == null)
                .Select(c => new { c.CheckoutId, c.EquipmentId })
                .ToListAsync();

            if (active.Count == 0)
            {
                return (0, 0);
            }

            var now = DateTime.Now;

            var lostCheckoutIds = active
                .Where(a => lostEquipmentIds.Contains(a.EquipmentId))
                .Select(a => a.CheckoutId)
                .ToList();

            var lostItemIds = active
                .Where(a => lostEquipmentIds.Contains(a.EquipmentId))
                .Select(a => a.EquipmentId)
                .ToList();

            var returnCheckoutIds = active
                .Where(a => !lostEquipmentIds.Contains(a.EquipmentId))
                .Select(a => a.CheckoutId)
                .ToList();

            var returnItemIds = active
                .Where(a => !lostEquipmentIds.Contains(a.EquipmentId))
                .Select(a => a.EquipmentId)
                .ToList();

            // Records the end date on each checkout. The records are kept for history.
            var returned = await _context.Checkouts
                .Where(c => returnCheckoutIds.Contains(c.CheckoutId) && c.ReturnDate == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReturnDate, now));

            var lost = await _context.Checkouts
                .Where(c => lostCheckoutIds.Contains(c.CheckoutId) && c.ReturnDate == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.ReturnDate, now)
                    .SetProperty(c => c.MarkedLost, true));

            // Returned items go back to Available and lost items are marked Lost,
            // keeping any other status an administrator already set
            await _context.Equipment
                .Where(e => returnItemIds.Contains(e.EquipmentId) && e.Status == CheckedOutStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, AvailableStatus));

            await _context.Equipment
                .Where(e => lostItemIds.Contains(e.EquipmentId) && e.Status == CheckedOutStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, "Lost"));

            // Records each lost item in its history (saved with the deactivation)
            if (lostItemIds.Count > 0)
            {
                var employee = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var employeeName = employee == null
                    ? "the employee"
                    : $"{employee.FirstName} {employee.LastName}";

                foreach (var equipmentId in lostItemIds)
                {
                    await _history.AddAsync(
                        equipmentId,
                        EquipmentHistoryService.MarkedLost,
                        $"Marked Lost when {employeeName}'s account was deactivated",
                        performedByUserId);
                }
            }

            return (returned, lost);
        }

        // Counts the employee's checkouts that have not been returned
        public Task<int> CountActiveCheckoutsAsync(int userId)
        {
            return _context.Checkouts.CountAsync(c =>
                c.UserId == userId && c.ReturnDate == null);
        }

        // Checks for an Approved reservation by someone else that covers today (Milestone 3, Reservations)
        private Task<bool> IsReservedForAnotherUserAsync(int equipmentId, int userId, DateTime today)
        {
            return _context.Reservations.AnyAsync(r =>
                r.EquipmentId == equipmentId &&
                r.UserId != userId &&
                r.Status == "Approved" &&
                r.StartDate <= today &&
                r.EndDate >= today);
        }

        private string LimitMessage =>
            $"You have reached the checkout limit of {_options.MaxActiveCheckouts} items. " +
            "Return an item before checking out another.";

        private const string NotCheckedOutToYouMessage =
            "This item is not checked out to you, or it has already been returned.";

        private const string ReservedMessage =
            "This item is reserved for another employee today.";

        private static string NotAvailableMessage(string status)
        {
            return status == "Retired"
                ? "This item has been retired and cannot be checked out."
                : $"This item is not available for checkout. Its status is {status}.";
        }

        // How long after a checkout or return a repeat of it is ignored
        private const int RepeatWindowMinutes = 1;

        private static CheckoutResult Repeat()
        {
            return new CheckoutResult { Succeeded = true, WasRepeat = true };
        }

        private static CheckoutResult Fail(string message)
        {
            return new CheckoutResult { Succeeded = false, Message = message };
        }
    }
}
