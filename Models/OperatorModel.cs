using asset_monitoring.Data;
using asset_monitoring.Services;
using asset_monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace asset_monitoring.Pages
{
    public class OperatorModel : AppPageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly PumpDashboardService _pumpDashboardService;
        private readonly ReportExportService _reportExportService;
        public string? LoggedInUserName { get; set; }
        public OperatorModel(ApplicationDbContext context, PumpDashboardService pumpDashboardService, ReportExportService reportExportService)
        {
            _context = context;
            _pumpDashboardService = pumpDashboardService;
            _reportExportService = reportExportService;
        }
        public List<DashboardPumpDto> Pumps { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            LoggedInUserName = Username;
            Pumps = await _pumpDashboardService.GetPumpsAsync(Username, UserType);
            return Page();
        }

        [BindProperty]
        public int PumpId { get; set; }
        [BindProperty]
        public string? VendorName { get; set; }
        [BindProperty]
        public string? Category { get; set; }
        [BindProperty]
        public string? LocationName { get; set; }
        [BindProperty]
        public string? Status { get; set; }
        [BindProperty]
        public bool IsActive { get; set; }
        [BindProperty]
        public string? Latitude { get; set; }
        [BindProperty]
        public string? Longitude { get; set; }
        

        public async Task<IActionResult> OnPostUpdatePumpAsync()
        {
            await _pumpDashboardService.UpdatePumpDetailsAsync(
                PumpId,
                VendorName ?? "",
                Category,
                LocationName ?? "",
                Status ?? "",
                Latitude,
                Longitude,
                IsActive
            );
            return new JsonResult(new { success = true });
        }
        public IActionResult OnPostLogout()
        {
           return LogoutAndRedirect(); //base
        }

        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            var pump = await _context.BdaPumpMasters.FindAsync(id);
            if (pump != null)
            {
                pump.IsActive = false;
                _context.BdaPumpMasters.Update(pump);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public IActionResult OnGetDownloadReport()
        {
            var allActivePumps = _pumpDashboardService.GetPumpsAsync().GetAwaiter().GetResult();
            return _reportExportService.ExportPumpsAsCsv(allActivePumps);
        }
    }
}