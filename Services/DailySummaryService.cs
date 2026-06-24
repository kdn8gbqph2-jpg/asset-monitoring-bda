using System.Collections.Concurrent;
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

        // Serializes summary rebuilds per IST date so concurrent builders (the
        // 5-min loop, the post-write fire-and-forget refresh, and startup backfill)
        // cannot interleave their delete+insert upsert and trip the
        // (pump_id, summary_date) unique key.
        private static readonly ConcurrentDictionary<DateTime, SemaphoreSlim> _dateLocks = new();

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

            // Get all active pumps along with their creation timestamps.
            // We cap each pump's "day start" at its creation time so a pump added
            // mid-day isn't credited with time that elapsed before it existed.
            var pumpInfo = await db.BdaPumpMasters
                .Where(p => p.IsActive)
                .Select(p => new { p.PumpId, p.RowInsertionDateTime })
                .ToListAsync();

            var pumpIds = pumpInfo.Select(p => p.PumpId).ToList();
            var pumpCreatedUtc = pumpInfo.ToDictionary(
                p => p.PumpId,
                p => DateTime.SpecifyKind(p.RowInsertionDateTime, DateTimeKind.Utc));

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
                    pumpLogs, currentEntries.GetValueOrDefault(pumpId),
                    pumpCreatedUtc[pumpId]);

                summaries.Add(summary);
            }

            // Upsert under a per-date lock: delete existing rows for this date,
            // then insert fresh. The lock prevents a concurrent rebuild of the
            // same date from interleaving its delete+insert (which would either
            // trip the (pump_id, summary_date) unique key or delete the other
            // run's freshly inserted rows). Last writer wins, which is correct —
            // both runs reconstruct from the same logs.
            var gate = _dateLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
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
            }
            finally
            {
                gate.Release();
            }

            Logger.Info("BuildSummaryForDateAsync: upserted {0} summaries for {1:yyyy-MM-dd}", summaries.Count, date);
        }

        /// <summary>
        /// Core computation: reconstruct the full timeline for a pump on a given day.
        ///
        /// Anchor-based algorithm (robust to a missing/dropped transition row):
        ///   Each log row carries two independently-trustworthy facts —
        ///     • at StartTime the pump was in OldStatus (start of that interval)
        ///     • at EndTime   the pump became NewStatus (the transition)
        ///   plus the live entry gives the open, not-yet-closed interval
        ///     • at CurrentStartTime the pump is in entry.Status (until "now").
        ///   These become (time, status) anchors; the status between one anchor and
        ///   the next is the earlier anchor's status. Because a surviving row's
        ///   OldStatus anchor still injects the correct status for its own interval,
        ///   a dropped ON→OFF row no longer makes the day replay a phantom ON period
        ///   (the previous algorithm used only NewStatus/EndTime and propagated the
        ///   wrong status across the gap).
        /// </summary>
        private PumpDailySummary ComputeDaySummary(
            int pumpId,
            DateTime date,
            DateTime dayStartUtc,
            DateTime dayEndUtc,
            DateTime effectiveDayEndUtc,
            List<PumpStatusLog> allPumpLogs,
            PumpStatusEntry? currentEntry,
            DateTime pumpCreatedUtc)
        {
            int onMinutes = 0, offMinutes = 0, maintenanceMinutes = 0;
            int statusChangeCount = 0;
            PumpStatus? firstStatus = null;
            PumpStatus? lastStatus  = null;

            // ── Cap the day start at pump creation time ─────────────────────
            // A pump that was added mid-day should not be credited with any
            // time before it existed. If the pump was created after the day
            // even began, there is simply nothing to compute for this date.
            if (pumpCreatedUtc > dayStartUtc)
                dayStartUtc = pumpCreatedUtc;

            if (dayStartUtc >= effectiveDayEndUtc)
            {
                return new PumpDailySummary
                {
                    PumpId               = pumpId,
                    SummaryDate          = date,
                    OnMinutes            = 0,
                    OffMinutes           = 0,
                    MaintenanceMinutes   = 0,
                    StatusChangeCount    = 0,
                    FirstStatus          = null,
                    LastStatus           = null,
                    RowInsertionDateTime = DateTime.UtcNow,
                    RowUpdationDateTime  = DateTime.UtcNow
                };
            }

            // ── Build (time, status) anchors from every surviving fact ───────
            // Anchors are inserted row-by-row (Start, then End) and finally the open
            // live interval. OrderBy is stable, so when two anchors share a timestamp
            // the later-inserted one wins — which lets the authoritative live status
            // take precedence over a stale chain tail.
            var anchors = new List<(DateTime Time, PumpStatus Status)>();
            foreach (var l in allPumpLogs)
            {
                if (l.StartTime.HasValue && l.OldStatus.HasValue)
                    anchors.Add((DateTime.SpecifyKind(l.StartTime.Value, DateTimeKind.Utc), l.OldStatus.Value));

                if (l.EndTime.HasValue)
                    anchors.Add((DateTime.SpecifyKind(l.EndTime.Value, DateTimeKind.Utc), l.NewStatus));
                else
                    // Legacy ongoing log (no EndTime): the transition to NewStatus was
                    // recorded at RowInsertionDateTime.
                    anchors.Add((DateTime.SpecifyKind(l.RowInsertionDateTime, DateTimeKind.Utc), l.NewStatus));
            }

            if (currentEntry?.CurrentStartTime != null)
                anchors.Add((DateTime.SpecifyKind(currentEntry.CurrentStartTime.Value, DateTimeKind.Utc),
                             currentEntry.Status));

            anchors = anchors.OrderBy(a => a.Time).ToList(); // stable on ties

            // ── Status at dayStart = latest anchor at or before dayStart ─────
            PumpStatus? statusAtDayStart = null;
            int firstAnchorInDay = anchors.Count; // first anchor strictly after dayStart
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Time <= dayStartUtc)
                {
                    statusAtDayStart = anchors[i].Status;
                }
                else
                {
                    firstAnchorInDay = i;
                    break;
                }
            }

            // Fallbacks when nothing is known before dayStart.
            if (statusAtDayStart == null)
                statusAtDayStart = anchors.Count > 0
                    ? anchors[0].Status                       // earliest known status
                    : (currentEntry?.Status ?? PumpStatus.Off);

            // ── Walk the day, accumulating minutes per status ────────────────
            // Each segment's minutes are computed as the difference of rounded
            // minute-offsets from dayStart. This telescopes exactly to the day's
            // total length (no per-segment truncation drift), so on+off+maint always
            // sums to the full day and the completeness check never falsely flags it.
            var cursor        = dayStartUtc;
            int accountedMin  = 0;
            var currentStatus = statusAtDayStart.Value;
            firstStatus       = currentStatus;
            lastStatus        = currentStatus;

            int PosOf(DateTime t) =>
                (int)Math.Round((t - dayStartUtc).TotalMinutes, MidpointRounding.AwayFromZero);

            for (int i = firstAnchorInDay; i < anchors.Count; i++)
            {
                var (time, status) = anchors[i];
                if (time >= effectiveDayEndUtc) break;

                if (time > cursor)
                {
                    int pos = PosOf(time);
                    AccumulateMinutes(currentStatus, pos - accountedMin,
                        ref onMinutes, ref offMinutes, ref maintenanceMinutes);
                    accountedMin = pos;
                    cursor = time;
                }

                if (status != currentStatus)
                {
                    statusChangeCount++;
                    currentStatus = status;
                    lastStatus    = status;
                }
            }

            // Remaining time from the last anchor to the end of the (effective) day.
            if (effectiveDayEndUtc > cursor)
            {
                int pos = PosOf(effectiveDayEndUtc);
                AccumulateMinutes(currentStatus, pos - accountedMin,
                    ref onMinutes, ref offMinutes, ref maintenanceMinutes);
            }

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
            try
            {
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
            catch (Exception ex)
            {
                Logger.Error(ex, "HasIncompleteSummaryAsync failed for date={0:yyyy-MM-dd}", date);
                return false; // Don't retry on error — will be caught next startup
            }
        }

    }
}
