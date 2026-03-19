using asset_monitoring.Services;
using asset_monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NLog;

namespace asset_monitoring.Pages
{
    public class OperatorModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly PumpDashboardService _pumpDashboardService;
        private readonly ReportExportService _reportExportService;
        public string? LoggedInUserName { get; set; }
        public OperatorModel(PumpDashboardService pumpDashboardService, ReportExportService reportExportService)
        {
            _pumpDashboardService = pumpDashboardService;
            _reportExportService = reportExportService;
        }
        public List<DashboardPumpDto> Pumps { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            LoggedInUserName = Username;
            Logger.Info("OnGetAsync: operator page loaded for userId={0}, user={1}", UserId, Username);
            Pumps = await _pumpDashboardService.GetPumpsAsync(UserId, UserType);
            Logger.Debug("OnGetAsync: loaded {0} pumps for operator userId={1}", Pumps.Count, UserId);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdatePumpAsync([FromBody] UpdatePumpRequest req)
        {
            Logger.Info("OnPostUpdatePumpAsync: pumpId={0} by operator userId={1}", req.PumpId, UserId);
            try
            {
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

        public IActionResult OnPostLogout()
        {
            Logger.Info("OnPostLogout: operator userId={0}, user={1} logged out", UserId, Username);
            return LogoutAndRedirect();
        }

        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            Logger.Info("OnPostDeletePumpAsync: soft-deleting pumpId={0} by operator userId={1}", id, UserId);
            await _pumpDashboardService.DeletePumpAsync(id);
            return RedirectToPage();
        }

        public IActionResult OnGetDownloadReport()
        {
            Logger.Info("OnGetDownloadReport: report download requested by operator userId={0}", UserId);
            var allActivePumps = _pumpDashboardService.GetPumpsAsync(UserId, UserType).GetAwaiter().GetResult();
            Logger.Info("OnGetDownloadReport: exporting {0} pumps to CSV", allActivePumps.Count);
            return _reportExportService.ExportPumpsAsCsv(allActivePumps);
        }
    }
}