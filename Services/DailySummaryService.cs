using asset_monitoring.Data;
using asset_monitoring.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Services
{
    /// <summary>
    /// Computes and persists daily pump running summaries from pump_status_log_tbl.
    ///
    /// Edge cases handled:
    ///   • Session spans midnight (e.g. ON from 23:00 → 03:00 splits across two days)
    ///   • Pump stays in same status for multiple days (no log entries on intermediate days)
    ///   • Pump has no log history at all (uses current status from pump_status_tbl)
    ///   • Currently-running session with no EndTime (uses "now" or dayEnd, whichever is earlier)
    ///   • Multiple status changes within a single day
    ///   • Summary for a future date is skipped
    /// </summary>
    public class DailySummaryService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IServiceScopeFactory _scopeFactory;

        private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");

        public DailySummaryService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Rebuild daily summary for a specific IST date.
        /// </summary>
        public async Task BuildSummaryForDateAsync(DateTime istDate)
        {
            var date = istDate.Date;
            var nowUtc = DateTime.UtcNow;
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Ist);

            // Don't compute summaries for future dates
            if (date > nowIst.Date)
            {
                Logger.Debug("BuildSummaryForDateAsync: skipping future date {0:yyyy-MM-dd}", date);
                return;
            }

            Logger.Info("BuildSummaryForDateAsync: computing for {0:yyyy-MM-dd}", date);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // IST day boundaries → UTC
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(date, Ist);
            var dayEndUtc   = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1), Ist);

            // Effective end of the day: either midnight or now (whichever is earlier)
            var effectiveDayEndUtc = nowUtc < dayEndUtc ? nowUtc : dayEndUtc;

            // Get all active pumps
            var pumpIds = await db.BdaPumpMasters
                .Where(p => p.IsActive)
                .Select(p => p.PumpId)
                .ToListAsync();

            // ── Fetch ALL log entries that could affect this day ──────────────
            // We need:
            //   1. The most recent log entry BEFORE dayStart (to know starting status)
            //   2. All log entries whose EndTime falls within the day (status changes during the day)
            //   3. Log entries that started before dayStart but ended during/after the day (cross-midnight)

            // Get all logs for these pumps up to dayEnd, ordered by EndTime
            var allRelevantLogs = await db.PumpStatusLogs
                .Where(l => pumpIds.Contains(l.PumpId)
                         && l.StartTime.HasValue)
                .OrderBy(l => l.PumpId)
                .ThenBy(l => l.RowInsertionDateTime)
                .AsNoTracking()
                .ToListAsync();

            // Current pump status entries (for live running sessions)
            var currentEntries = await db.PumpStatusEntries
                .Where(e => pumpIds.Contains(e.PumpId))
                .AsNoTracking()
                .ToDictionaryAsync(e => e.PumpId);

            var summaries = new List<PumpDailySummary>();

            foreach (var pumpId in pumpIds)
            {
                var pumpLogs = allRelevantLogs
                    .Where(l => l.PumpId == pumpId)
                    .ToList();

                var summary = ComputeDaySummary(
                    pumpId, date, dayStartUtc, dayEndUtc, effectiveDayEndUtc,
                    pumpLogs, currentEntries.GetValueOrDefault(pumpId));

                summaries.Add(summary);
            }

            // Upsert: delete existing rows for this date, then insert fresh
            var existingRows = await db.PumpDailySummaries
                .Where(s => s.SummaryDate == date && pumpIds.Contains(s.PumpId))
                .ToListAsync();

            if (existingRows.Any())
            {
                db.PumpDailySummaries.RemoveRange(existingRows);
                await db.SaveChangesAsync();
            }

            db.PumpDailySummaries.AddRange(summaries);
            await db.SaveChangesAsync();

            Logger.Info("BuildSummaryForDateAsync: upserted {0} summaries for {1:yyyy-MM-dd}", summaries.Count, date);
        }

        /// <summary>
        /// Core computation: reconstruct the full timeline for a pump on a given day.
        ///
        /// Algorithm:
        ///   1. Determine what status the pump was in at dayStart
        ///      → Look at the most recent log entry whose EndTime ≤ dayStart; take its NewStatus
        ///      → If no such log, use the current status from pump_status_tbl (it never changed)
        ///   2. Collect all "status change events" that happened during the day
        ///      → A change event happens at EndTime of a log entry (that's when NewStatus takes effect)
        ///   3. Walk the timeline from dayStart to effectiveDayEnd:
        ///      → From dayStart to first change: accumulate in the starting status
        ///      → Between changes: accumulate in the status that was set by the previous change
        ///      → From last change to dayEnd: accumulate in the final status
        /// </summary>
        private PumpDailySummary ComputeDaySummary(
            int pumpId,
            DateTime date,
            DateTime dayStartUtc,
            DateTime dayEndUtc,
            DateTime effectiveDayEndUtc,
            List<PumpStatusLog> allPumpLogs,
            PumpStatusEntry? currentEntry)
        {
            int onMinutes = 0, offMinutes = 0, maintenanceMinutes = 0;
            int statusChangeCount = 0;
            PumpStatus? firstStatus = null;
            PumpStatus? lastStatus  = null;

            // ── Step 1: Determine starting status at dayStart ────────────────
            // Find the most recent log entry that ended ON or BEFORE dayStart
            // Its NewStatus tells us what the pump was set to before this day began
            var lastLogBeforeDay = allPumpLogs
                .Where(l => l.EndTime.HasValue && l.EndTime.Value <= dayStartUtc)
                .OrderByDescending(l => l.EndTime)
                .FirstOrDefault();

            PumpStatus? statusAtDayStart = null;

            if (lastLogBeforeDay != null)
            {
                // The pump was changed to this status before today
                statusAtDayStart = lastLogBeforeDay.NewStatus;
            }
            else
            {
                // No log entry ended before this day. Two possibilities:
                // a) There's an ongoing session that started before this day (log with StartTime but no EndTime)
                // b) Pump has never changed status — use current status from pump_status_tbl
                var ongoingBeforeDay = allPumpLogs
                    .Where(l => l.StartTime.HasValue
                             && l.StartTime.Value <= dayStartUtc
                             && !l.EndTime.HasValue)
                    .OrderByDescending(l => l.StartTime)
                    .FirstOrDefault();

                if (ongoingBeforeDay != null)
                {
                    // Pump was in OldStatus since before this day (session still running)
                    statusAtDayStart = ongoingBeforeDay.OldStatus ?? ongoingBeforeDay.NewStatus;
                }
                else if (currentEntry != null)
                {
                    // Pump never had a status change, use whatever it is now
                    statusAtDayStart = currentEntry.Status;
                }
                // else: pump has no status info at all — leave as null
            }

            // ── Step 2: Collect status change events within the day ───────────
            // A "change event" = a log entry whose EndTime falls within [dayStart, dayEnd)
            // At EndTime, the pump transitions from OldStatus → NewStatus
            var changesInDay = allPumpLogs
                .Where(l => l.EndTime.HasValue
                         && l.EndTime.Value > dayStartUtc
                         && l.EndTime.Value < dayEndUtc)
                .OrderBy(l => l.EndTime)
                .ToList();

            // Also check for sessions that STARTED during the day but have no EndTime
            // (pump turned ON during the day and is still running)
            // These are captured by log entries where:
            //   - OldStatus changed to NewStatus
            //   - The change happened (EndTime) during the day... but wait,
            //     if there's no EndTime, the OLD status session is still active.
            // Actually, log entries with no EndTime mean the OLD status session hasn't ended yet.
            // So we check: is there an ongoing log (no EndTime) whose StartTime < dayEnd?
            // If so, the OldStatus was active from StartTime through the day.

            // ── Step 3: Walk the timeline ────────────────────────────────────
            var cursor = dayStartUtc;
            var currentStatus = statusAtDayStart;
            firstStatus = currentStatus;

            foreach (var change in changesInDay)
            {
                var changeTime = change.EndTime!.Value;

                // Accumulate time from cursor to changeTime in currentStatus
                if (currentStatus.HasValue && changeTime > cursor)
                {
                    var minutes = (int)(changeTime - cursor).TotalMinutes;
                    AccumulateMinutes(currentStatus.Value, minutes,
                        ref onMinutes, ref offMinutes, ref maintenanceMinutes);
                }

                // Transition to new status
                currentStatus = change.NewStatus;
                lastStatus = change.NewStatus;
                cursor = changeTime;
                statusChangeCount++;
            }

            // ── Step 4: Fill remaining time from last change to end of day ───
            if (currentStatus.HasValue && effectiveDayEndUtc > cursor)
            {
                var minutes = (int)(effectiveDayEndUtc - cursor).TotalMinutes;
                AccumulateMinutes(currentStatus.Value, minutes,
                    ref onMinutes, ref offMinutes, ref maintenanceMinutes);
            }

            // If no changes happened, lastStatus = firstStatus
            if (lastStatus == null)
                lastStatus = firstStatus;

            return new PumpDailySummary
            {
                PumpId               = pumpId,
                SummaryDate          = date,
                OnMinutes            = onMinutes,
                OffMinutes           = offMinutes,
                MaintenanceMinutes   = maintenanceMinutes,
                StatusChangeCount    = statusChangeCount,
                FirstStatus          = firstStatus,
                LastStatus           = lastStatus,
                RowInsertionDateTime = DateTime.UtcNow,
                RowUpdationDateTime  = DateTime.UtcNow
            };
        }

        private static void AccumulateMinutes(PumpStatus status, int minutes,
            ref int onMin, ref int offMin, ref int maintMin)
        {
            switch (status)
            {
                case PumpStatus.On:          onMin    += minutes; break;
                case PumpStatus.Off:         offMin   += minutes; break;
                case PumpStatus.Maintenance: maintMin += minutes; break;
            }
        }

        /// <summary>
        /// Check if a past date has incomplete summaries (total != 1440 for any pump, or missing pumps).
        /// </summary>
        public async Task<bool> HasIncompleteSummaryAsync(DateTime istDate)
        {
            var date = istDate.Date;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var activePumpCount = await db.BdaPumpMasters.CountAsync(p => p.IsActive);
            var summaries = await db.PumpDailySummaries
                .Where(s => s.SummaryDate == date)
                .ToListAsync();

            // Missing entirely or missing some pumps
            if (summaries.Count < activePumpCount)
                return true;

            // Check if any summary has total != 1440 (partial day)
            return summaries.Any(s =>
            {
                var total = s.OnMinutes + s.OffMinutes + s.MaintenanceMinutes;
                return total < 1438; // Allow 2-min rounding tolerance
            });
        }

        /// <summary>
        /// Rebuild summaries for a range of dates (backfill).
        /// </summary>
        public async Task BackfillAsync(DateTime istStartDate, DateTime istEndDate)
        {
            Logger.Info("BackfillAsync: from {0:yyyy-MM-dd} to {1:yyyy-MM-dd}", istStartDate, istEndDate);
            for (var d = istStartDate.Date; d <= istEndDate.Date; d = d.AddDays(1))
            {
                await BuildSummaryForDateAsync(d);
            }
        }
    }
}
