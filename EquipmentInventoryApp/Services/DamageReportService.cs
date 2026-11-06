
using EquipmentInventoryApp.Data;
using EquipmentInventoryApp.Models;
using EquipmentInventoryApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EquipmentInventoryApp.Services
{
    // One open report on the Damage Reports page (FR-33)
    public class DamageReportRow
    {
        public int DamageReportId { get; set; }

        public int EquipmentId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string SerialNumber { get; set; } = string.Empty;

        public string ItemStatus { get; set; } = string.Empty;

        public string ReporterName { get; set; } = string.Empty;

        public DateTime ReportDate { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    // Damage report rules (US-29, US-30, FR-33 to FR-35, Milestone 3 Damage Reports)
    public class DamageReportService
    {
        public const int PageSize = 10;

        public const string OpenStatus = "Open";
        public const string ClosedStatus = "Closed";

        // Field specification Dmg-1: required, 10 to 500 characters
        public const int DescriptionMinLength = 10;
        public const int DescriptionMaxLength = 500;
        public const string DescriptionMessage = "Please describe the problem (10–500 characters)";

        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;
        private readonly EquipmentHistoryService _history;
        private readonly EquipmentStatusService _statusService;

        public DamageReportService(
            ApplicationDbContext context,
            NotificationService notifications,
            EquipmentHistoryService history,
            EquipmentStatusService statusService)
        {
            _context = context;
            _notifications = notifications;
            _history = history;
            _statusService = statusService;
        }

        // Saves a report as Open and notifies the administrators in the same transaction.
        // The item's status does not change, so one report cannot take an item out of service.
        public async Task<StatusChangeResult> SubmitAsync(int equipmentId, int reporterId, string? description)
        {
            var text = description?.Trim() ?? string.Empty;

            if (text.Length < DescriptionMinLength || text.Length > DescriptionMaxLength)
            {
                return Fail(DescriptionMessage);
            }

            var equipment = await _context.Equipment
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EquipmentId == equipmentId);

            if (equipment == null)
            {
                return Fail("Item not found.");
            }

            if (equipment.Status == "Retired")
            {
                return Fail("This item has been retired, so damage cannot be reported.");
            }

            var reporter = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == reporterId);

            var reporterName = reporter == null ? "An employee" : $"{reporter.FirstName} {reporter.LastName}";

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.DamageReports.Add(new DamageReport
            {
                EquipmentId = equipmentId,
                UserId = reporterId,
                Description = text,
                ReportDate = DateTime.Now,
                Status = OpenStatus
            });

            await _notifications.AddForAdministratorsAsync(
                $"Damage reported for {equipment.Name} ({equipment.SerialNumber}) by {reporterName}: " +
                $"\"{Shorten(text, 150)}\" See Damage Reports to review it.");

            await _history.AddAsync(
                equipmentId,
                "Damage Reported",
                text,
                reporterId);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok("Damage report submitted. An administrator will review it.");
        }

        // Open reports, newest first, one page at a time (FR-33)
        public async Task<PagedResult<DamageReportRow>> GetOpenAsync(int page)
        {
            var reports = _context.DamageReports
                .Where(d => d.Status == OpenStatus);

            var totalCount = await reports.CountAsync(d => true);

            page = PagedResult<DamageReportRow>.ClampPage(page, totalCount, PageSize);

            var records = await reports
                .Include(d => d.Equipment)
                .Include(d => d.User)
                .AsNoTracking()
                .OrderByDescending(d => d.ReportDate)
                .ThenByDescending(d => d.DamageReportId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return new PagedResult<DamageReportRow>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = totalCount,
                Items = records
                    .Select(d => new DamageReportRow
                    {
                        DamageReportId = d.DamageReportId,
                        EquipmentId = d.EquipmentId,
                        ItemName = d.Equipment.Name,
                        SerialNumber = d.Equipment.SerialNumber,
                        ItemStatus = d.Equipment.Status,
                        ReporterName = $"{d.User.FirstName} {d.User.LastName}",
                        ReportDate = d.ReportDate,
                        Description = d.Description
                    })
                    .ToList()
            };
        }

        // Sets the reported item to Under Repair (FR-34). The report's description becomes
        // the repair note. The report stays open until Close Report is selected.
        public async Task<StatusChangeResult> MarkUnderRepairAsync(int reportId, int administratorId)
        {
            var report = await _context.DamageReports
                .Include(d => d.Equipment)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DamageReportId == reportId);

            if (report == null || report.Status != OpenStatus)
            {
                return Fail("This damage report is no longer open.");
            }

            if (report.Equipment.Status == "Under Repair")
            {
                return Ok($"{report.Equipment.Name} is already Under Repair.");
            }

            var note = Shorten($"Damage report: {report.Description}", 255);

            return await _statusService.ChangeStatusAsync(
                report.EquipmentId,
                "Under Repair",
                note,
                administratorId);
        }

        // Closes the report and records the closing date (FR-35).
        // The item's status is changed separately when it is ready to return to service.
        public async Task<StatusChangeResult> CloseAsync(int reportId, int administratorId)
        {
            var report = await _context.DamageReports
                .Include(d => d.Equipment)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DamageReportId == reportId);

            if (report == null)
            {
                return Fail("Damage report not found.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Closes the report only if it is still open
            var closed = await _context.DamageReports
                .Where(d => d.DamageReportId == reportId && d.Status == OpenStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, ClosedStatus)
                    .SetProperty(d => d.CloseDate, DateTime.Now));

            if (closed == 0)
            {
                await transaction.RollbackAsync();

                return Fail("This damage report is no longer open.");
            }

            await _history.AddAsync(
                report.EquipmentId,
                "Damage Report Closed",
                Shorten(report.Description, 200),
                administratorId);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok($"Damage report for {report.Equipment.Name} closed.");
        }

        private static string Shorten(string text, int maxLength)
        {
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
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
