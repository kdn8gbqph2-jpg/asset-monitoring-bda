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

            if (UserType != "BDA_OFFICIAL" && UserType != "ADMIN")
            {
                Logger.Warn("JuniorEngineer OnGetAsync: unauthorized access by userType={0}", UserType);
                return RedirectToPage("/Index");
            }

            LoggedInUserName = Username;
            Logger.Info("JuniorEngineer OnGetAsync: user={0}", Username);

            // ADMIN sees all pumps; BDA_OFFICIAL sees all pumps in read-only mode
            Pumps = await _pumpService.GetPumpsAsync(UserId, UserType == "ADMIN" ? "ADMIN" : "OPERATOR");

            return Page();
        }

        // ── Assign operator to a pump ──────────────────────────────────────
        public async Task<JsonResult> OnPostAssignOperatorAsync([FromBody] AssignOperatorRequest req)
        {
            Logger.Info("JuniorEngineer OnPostAssignOperatorAsync: pumpId={0}, operator={1}",
                req.PumpId, req.OperatorMobile);

            if (!IsLoggedIn || (UserType != "BDA_OFFICIAL" && UserType != "ADMIN"))
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            try
            {
                var ok = await _pumpService.AssignOperatorAsync(req);
                return new JsonResult(new { success = ok });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JuniorEngineer OnPostAssignOperatorAsync failed");
                return new JsonResult(new { success = false, message = "Server error" }) { StatusCode = 500 };
            }
        }

        public IActionResult OnGetDownloadReport()
        {
            var pumps = _pumpService.GetPumpsAsync().GetAwaiter().GetResult();
            return _reportExportService.ExportPumpsAsCsv(pumps);
        }

        public IActionResult OnGetDownloadReportXlsx()
        {
            var pumps = _pumpService.GetPumpsAsync().GetAwaiter().GetResult();
            return _reportExportService.ExportPumpsAsXlsx(pumps);
        }

        public IActionResult OnGetDownloadReportPdf()
        {
            var pumps = _pumpService.GetPumpsAsync().GetAwaiter().GetResult();
            return _reportExportService.ExportPumpsAsPdf(pumps);
        }
    }
}
