using NLog;

namespace asset_monitoring.Services
{
    /// <summary>
    /// Background service that:
    ///   1. Rebuilds yesterday's daily summary at midnight IST (00:05).
    ///   2. Refreshes today's summary every 15 minutes so it stays current.
    /// </summary>
    public class DailySummaryBackgroundService : BackgroundService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DailySummaryService _summaryService;

        private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");

        public DailySummaryBackgroundService(DailySummaryService summaryService)
        {
            _summaryService = summaryService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.Info("DailySummaryBackgroundService started");

            // On startup: backfill any incomplete past days (up to 30 days back)
            await BackfillIncompleteDays(stoppingToken);

            // Initial build on startup — today + yesterday
            await SafeBuild(DateTime.UtcNow, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                    await SafeBuild(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "DailySummaryBackgroundService: error in loop");
                    // Wait a bit before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            Logger.Info("DailySummaryBackgroundService stopped");
        }

        /// <summary>
        /// On startup, find past days where total minutes != 1440 and rebuild them.
        /// This handles cases where the app was down and summaries are stale/partial.
        /// </summary>
        private async Task BackfillIncompleteDays(CancellationToken ct)
        {
            try
            {
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist);
                var today = nowIst.Date;

                // Check last 30 days (excluding today — today is always partial)
                for (int i = 1; i <= 30; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    var date = today.AddDays(-i);
                    var isIncomplete = await _summaryService.HasIncompleteSummaryAsync(date);
                    if (isIncomplete)
                    {
                        Logger.Info("Backfilling incomplete summary for {0}", date.ToString("yyyy-MM-dd"));
                        await _summaryService.BuildSummaryForDateAsync(date);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "BackfillIncompleteDays failed");
            }
        }

        private async Task SafeBuild(DateTime nowUtc, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var nowIst = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Ist);
                var today = nowIst.Date;
                var yesterday = today.AddDays(-1);

                // Always rebuild yesterday (catches any late log entries)
                await _summaryService.BuildSummaryForDateAsync(yesterday);

                // Rebuild today (includes live running sessions)
                await _summaryService.BuildSummaryForDateAsync(today);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "DailySummaryBackgroundService.SafeBuild failed");
            }
        }
    }
}
