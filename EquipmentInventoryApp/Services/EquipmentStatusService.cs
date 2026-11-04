
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // The outcome of a status change
    public class StatusChangeResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    // Rules for changing an item's status and retiring it (US-9, US-10, FR-10, FR-11)
    public class EquipmentStatusService
    {
        private const string CheckedOutStatus = "Checked Out";
        private const string LostStatus = "Lost";
        private const string RetiredStatus = "Retired";

        private readonly ApplicationDbContext _context;
        private readonly EquipmentHistoryService _history;

        public EquipmentStatusService(
            ApplicationDbContext context,
            EquipmentHistoryService history)
        {
            _context = context;
            _history = history;
        }

        // Changes the status to Available, Under Repair, or Lost.
        // A checked-out item can only be marked Lost, which closes the employee's checkout.
        // Under Repair requires a note of up to 255 characters explaining why.
        public async Task<StatusChangeResult> ChangeStatusAsync(
            int equipmentId,
            string newStatus,
            string? repairNote,
            int? performedByUserId)
        {
            if (!EquipmentOptions.AdminStatuses.Contains(newStatus))
            {
                return Fail("Please select a valid status.");
            }

            var isUnderRepair = newStatus == EquipmentOptions.UnderRepairStatus;
            var note = repairNote?.Trim() ?? string.Empty;

            if (isUnderRepair && note.Length == 0)
            {
                return Fail(EquipmentOptions.RepairNoteRequiredMessage + ".");
            }

            if (isUnderRepair && note.Length > EquipmentOptions.RepairNoteMaxLength)
            {
                return Fail(EquipmentOptions.RepairNoteTooLongMessage + ".");
            }

            // The note is kept only while the item is Under Repair
            string? noteToSave = isUnderRepair ? note : null;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return Fail("Item not found.");
            }

            if (equipment.Status == newStatus)
            {
                // An item already Under Repair can have its note updated
                if (isUnderRepair && equipment.RepairNote != noteToSave)
                {
                    await _context.Equipment
                        .Where(e => e.EquipmentId == equipmentId)
                        .ExecuteUpdateAsync(s => s.SetProperty(e => e.RepairNote, noteToSave));

                    await _history.AddAsync(
                        equipmentId,
                        EquipmentHistoryService.RepairNoteUpdated,
                        noteToSave,
                        performedByUserId);

                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Ok($"Repair note updated for {equipment.Name}.");
                }

                return Ok($"{equipment.Name} is already {newStatus}.");
            }

            if (equipment.Status == RetiredStatus)
            {
                return Fail("Retired items cannot be changed.");
            }

            // Saved before the change so the history shows the old status
            var oldStatus = equipment.Status;

            var activeCheckout = await GetActiveCheckoutAsync(equipmentId);

            if (activeCheckout != null && newStatus != LostStatus)
            {
                return Fail(
                    $"This item is checked out to {FullName(activeCheckout.User)}. " +
                    "It can be marked Lost now, or changed to another status after it is returned.");
            }

            // Changes the status only if nobody else changed it first
            var changed = await _context.Equipment
                .Where(e => e.EquipmentId == equipmentId && e.Status == oldStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, newStatus)
                    .SetProperty(e => e.RepairNote, noteToSave));

            if (changed == 0)
            {
                await transaction.RollbackAsync();

                return Fail("This item was just changed by someone else. Please try again.");
            }

            // Ends the employee's checkout as lost, so it no longer counts against them.
            // The record is kept for history.
            if (activeCheckout != null)
            {
                await CloseCheckoutAsLostAsync(activeCheckout.CheckoutId);
            }

            // Records the change in the item's history
            var details = $"{oldStatus} to {newStatus}";

            if (isUnderRepair)
            {
                details += $": {noteToSave}";
            }

            if (activeCheckout != null)
            {
                details += $". Checkout by {FullName(activeCheckout.User)} was closed.";
            }

            await _history.AddAsync(
                equipmentId,
                EquipmentHistoryService.StatusChanged,
                details,
                performedByUserId);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(activeCheckout == null
                ? $"{equipment.Name} status changed to {newStatus}."
                : $"{equipment.Name} was marked Lost. {FullName(activeCheckout.User)}'s checkout was closed.");
        }

        // Retires an item without deleting it or its history (FR-11).
        // A checked-out item must be returned or marked Lost first.
        public async Task<StatusChangeResult> RetireAsync(int equipmentId, int? performedByUserId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return Fail("Item not found.");
            }

            if (equipment.Status == RetiredStatus)
            {
                return Fail($"{equipment.Name} is already retired.");
            }

            var activeCheckout = await GetActiveCheckoutAsync(equipmentId);

            if (activeCheckout != null)
            {
                return Fail(RetireBlockedMessage(activeCheckout));
            }

            // Saved before the change so the history shows the old status
            var statusBeforeRetiring = equipment.Status;

            var changed = await _context.Equipment
                .Where(e => e.EquipmentId == equipmentId && e.Status == statusBeforeRetiring)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Status, RetiredStatus)
                    .SetProperty(e => e.RepairNote, (string?)null));

            if (changed == 0)
            {
                await transaction.RollbackAsync();

                return Fail("This item was just changed by someone else. Please try again.");
            }

            await _history.AddAsync(
                equipmentId,
                EquipmentHistoryService.Retired,
                $"Removed from service (was {statusBeforeRetiring})",
                performedByUserId);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok($"{equipment.Name} has been retired. Its history has been kept.");
        }

        // Gets the item's checkout that has not ended, with the employee who has it
        public Task<Checkout?> GetActiveCheckoutAsync(int equipmentId)
        {
            return _context.Checkouts
                .Include(c => c.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.EquipmentId == equipmentId && c.ReturnDate == null);
        }

        public static string RetireBlockedMessage(Checkout activeCheckout)
        {
            return $"This item is checked out to {FullName(activeCheckout.User)}. " +
                   "It must be returned or marked Lost before it can be retired.";
        }

        private Task<int> CloseCheckoutAsLostAsync(int checkoutId)
        {
            return _context.Checkouts
                .Where(c => c.CheckoutId == checkoutId && c.ReturnDate == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.ReturnDate, DateTime.Now)
                    .SetProperty(c => c.MarkedLost, true));
        }

        private static string FullName(User user)
        {
            return $"{user.FirstName} {user.LastName}";
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
