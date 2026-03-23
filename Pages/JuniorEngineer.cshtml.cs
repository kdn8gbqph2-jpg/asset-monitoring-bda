using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace asset_monitoring.Pages
{
    public class JuniorEngineerModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly PumpDashboardService _pumpService;
        private readonly ReportExportService  _reportExportService;

        public List<DashboardPumpDto> Pumps { get; private set; } = new();
        public List<PumpRunningSummaryDto> RunningSummary { get; private set; } = new();
        public string? LoggedInUserName { get; private set; }

        public JuniorEngineerModel(
            PumpDashboardService pumpService,
            ReportExportService  reportExportService)
        {
            _pumpService         = pumpService;
            _reportExportService = reportExportService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!IsLoggedIn)
            {
                Logger.Warn("JuniorEngineer OnGetAsync: unauthenticated access");
                return RedirectToPage("/Index");
            }

            if (UserType != "JE" && UserType != "ADMIN")
            {
                Logger.Warn("JuniorEngineer OnGetAsync: unauthorized access by userType={0}", UserType);
                return RedirectToPage("/Index");
            }

            LoggedInUserName = Username;
            Logger.Info("JuniorEngineer OnGetAsync: user={0}", Username);

            // ADMIN sees all pumps; JE sees pumps assigned to them via je_mobile
            Pumps = await _pumpService.GetPumpsAsync(UserId, UserType);
            RunningSummary = await _pumpService.GetPumpRunningSummaryAsync(UserId, UserType);

            return Page();
        }

        // ── Update pump details ────────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdatePumpAsync([FromBody] UpdatePumpRequest req)
        {
            Logger.Info("JuniorEngineer OnPostUpdatePumpAsync: pumpId={0} by user={1}", req.PumpId, Username);

            if (!IsLoggedIn || (UserType != "JE" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            try
            {
                req.UpdatedBy = Username;
                await _pumpService.UpdatePumpDetailsAsync(req);
                Logger.Info("JuniorEngineer OnPostUpdatePumpAsync: pumpId={0} updated successfully", req.PumpId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JuniorEngineer OnPostUpdatePumpAsync failed for pumpId={0}", req.PumpId);
                return new JsonResult(new { success = false, message = "Update failed" }) { StatusCode = 500 };
            }
        }

        public async Task<JsonResult> OnGetActiveUsersAsync()
        {
            if (!IsLoggedIn || (UserType != "JE" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" });

            try
            {
                var users = await _pumpService.GetActiveUsersForDrawerAsync();
                return new JsonResult(users);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JuniorEngineer OnGetActiveUsersAsync failed");
                return new JsonResult(new { success = false, message = "Failed to load users" });
            }
        }

        public IActionResult OnPostLogout()
        {
            Logger.Info("JuniorEngineer OnPostLogout: user={0} logged out", Username);
            return LogoutAndRedirect();
        }

        public async Task<IActionResult> OnGetDownloadReportAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            try
            {
                var pumps = await _pumpService.GetPumpsAsync(UserId, UserType);
                Logger.Info("JE OnGetDownloadReportAsync: exporting {0} pumps as CSV", pumps.Count);
                return _reportExportService.ExportPumpsAsCsv(pumps);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JE OnGetDownloadReportAsync failed");
                return StatusCode(500, "Failed to generate report");
            }
        }

        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            try
            {
                var pumps = await _pumpService.GetPumpsAsync(UserId, UserType);
                Logger.Info("JE OnGetDownloadReportXlsxAsync: exporting {0} pumps as XLSX", pumps.Count);
                return _reportExportService.ExportPumpsAsXlsx(pumps);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JE OnGetDownloadReportXlsxAsync failed");
                return StatusCode(500, "Failed to generate report");
            }
        }

        public async Task<IActionResult> OnGetDownloadReportPdfAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            try
            {
                var pumps = await _pumpService.GetPumpsAsync(UserId, UserType);
                Logger.Info("JE OnGetDownloadReportPdfAsync: exporting {0} pumps as PDF", pumps.Count);
                return _reportExportService.ExportPumpsAsPdf(pumps);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JE OnGetDownloadReportPdfAsync failed");
                return StatusCode(500, "Failed to generate report");
            }
        }
    }
}
