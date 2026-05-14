using asset_monitoring.Services;
using asset_monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace asset_monitoring.Pages
{
    public class OperatorModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly PumpDashboardService _pumpDashboardService;
        private readonly ReportExportService _reportExportService;

        public List<DashboardPumpDto> Pumps { get; set; } = new();
        public PumpRunningSummaryResult RunningSummary { get; set; } = new();
        public string? LoggedInUserName { get; set; }

        public OperatorModel(PumpDashboardService pumpDashboardService, ReportExportService reportExportService)
        {
            _pumpDashboardService = pumpDashboardService;
            _reportExportService  = reportExportService;
        }

        public async Task<IActionResult> OnGetAsync(int? summaryYear = null, int? summaryMonth = null)
        {
            if (!IsLoggedIn)
            {
                Logger.Warn("OnGetAsync: unauthenticated access attempt to operator page");
                return RedirectToPage("/Index");
            }

            if (UserType != "OPERATOR" && UserType != "ADMIN")
            {
                Logger.Warn("OnGetAsync: unauthorized access to operator page by userType={0}", UserType);
                return RedirectToPage("/Index");
            }

            LoggedInUserName = Username;
            Logger.Info("OnGetAsync: operator page loaded for userId={0}, user={1}", UserId, Username);

            Pumps          = await _pumpDashboardService.GetPumpsAsync(UserId, UserType);
            RunningSummary = await _pumpDashboardService.GetPumpRunningSummaryAsync(
                UserId, UserType, summaryYear, summaryMonth);

            Logger.Debug("OnGetAsync: loaded {0} pumps for operator userId={1}", Pumps.Count, UserId);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdatePumpAsync([FromBody] UpdatePumpRequest req)
        {
            if (!IsLoggedIn || (UserType != "OPERATOR" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostUpdatePumpAsync: pumpId={0} by operator userId={1}", req.PumpId, UserId);
            try
            {
                req.UpdatedBy = Username;
                await _pumpDashboardService.UpdatePumpDetailsAsync(req);
                Logger.Info("OnPostUpdatePumpAsync: pumpId={0} updated successfully", req.PumpId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostUpdatePumpAsync failed for pumpId={0}", req.PumpId);
                return new JsonResult(new { success = false, message = "Update failed" });
            }
        }

        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            if (!IsLoggedIn || (UserType != "OPERATOR" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostDeletePumpAsync: soft-deleting pumpId={0} by operator userId={1}", id, UserId);
            try
            {
                await _pumpDashboardService.DeletePumpAsync(id);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostDeletePumpAsync failed for pumpId={0}", id);
                return new JsonResult(new { success = false, message = "Delete failed" }) { StatusCode = 500 };
            }
        }

        public async Task<JsonResult> OnGetActiveUsersAsync()
        {
            if (!IsLoggedIn || (UserType != "OPERATOR" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" });

            try
            {
                var users = await _pumpDashboardService.GetActiveUsersForDrawerAsync();
                return new JsonResult(users);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Operator OnGetActiveUsersAsync failed");
                return new JsonResult(new { success = false, message = "Failed to load users" });
            }
        }

        public async Task<IActionResult> OnGetDownloadReportAsync()
        {
            var pumps = await _pumpDashboardService.GetPumpsAsync(UserId, UserType);
            Logger.Info("OnGetDownloadReport: exporting {0} pumps as CSV, operator userId={1}", pumps.Count, UserId);
            return _reportExportService.ExportPumpsAsCsv(pumps);
        }

        // ── Running Summary downloads (CSV / XLSX / PDF) ─────────────────────
        public Task<IActionResult> OnGetDownloadRunningSummaryCsvAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("CSV", summaryYear, summaryMonth);
        public Task<IActionResult> OnGetDownloadRunningSummaryXlsxAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("XLSX", summaryYear, summaryMonth);
        public Task<IActionResult> OnGetDownloadRunningSummaryPdfAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("PDF", summaryYear, summaryMonth);

        private async Task<IActionResult> ExportRunningSummary(string format, int? year, int? month)
        {
            if (!IsLoggedIn || (UserType != "OPERATOR" && UserType != "ADMIN"))
                return RedirectToPage("/Index");
            try
            {
                var summary = await _pumpDashboardService.GetPumpRunningSummaryAsync(
                    UserId, UserType, year, month);
                Logger.Info("Operator ExportRunningSummary({0}): {1} rows, period={2}-{3}, userId={4}",
                    format, summary.Rows.Count, summary.Year, summary.Month, UserId);
                return format switch
                {
                    "XLSX" => _reportExportService.ExportRunningSummaryAsXlsx(summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                    "PDF"  => _reportExportService.ExportRunningSummaryAsPdf (summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                    _      => _reportExportService.ExportRunningSummaryAsCsv (summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Operator ExportRunningSummary({0}) failed", format);
                return StatusCode(500, $"Failed to generate running summary {format}");
            }
        }

        public IActionResult OnPostLogout()
        {
            Logger.Info("OnPostLogout: operator userId={0}, user={1} logged out", UserId, Username);
            return LogoutAndRedirect();
        }
    }
}
