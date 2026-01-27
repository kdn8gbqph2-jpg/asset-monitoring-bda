using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace asset_monitoring.Pages
{
    public class AdminModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserCacheService _userCache;
        private readonly PumpDashboardService _pumpDashboardService;
        private readonly ReportExportService _reportExportService;

        public AdminModel(ApplicationDbContext context, UserCacheService userCache, PumpDashboardService pumpDashboardService, ReportExportService reportExportService)
        {
            _context = context;
            _userCache = userCache;
            _pumpDashboardService = pumpDashboardService;
            _reportExportService = reportExportService;
        }

        public List<BdaUserMaster> Users { get; set; } = new();
        public List<DashboardPumpDto> Pumps { get; set; } = new();
        public List<BdaPumpLocation> Locations { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string? LoggedInUserName { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userType = HttpContext.Session.GetString("UserType");
            IsAdmin = userType == "ADMIN";
            LoggedInUserName = HttpContext.Session.GetString("UserName"); // Add this line

            if (!IsAdmin)
                return Page();

            Users = await _context.BdaUserMasters
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();

            // No username filter for admin: fetch all active pumps with mobile number
            Pumps = await _pumpDashboardService.GetPumpsAsync();

            Locations = await _context.BdaPumpLocations
                .AsNoTracking()
                .ToListAsync();

            return Page();
        }

        // Add handlers for pump edit/delete as needed
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

        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            var pump = await _context.BdaPumpMasters.FindAsync(id);
            if (pump != null)
            {
                // Soft delete if you have IsActive, else remove
                pump.IsActive = false;
                _context.BdaPumpMasters.Update(pump);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdatePumpAsync()
        {
            await _pumpDashboardService.UpdatePumpDetailsAsync(
                PumpId,
                VendorName ?? "",
                Category,
                LocationName ?? "",
                Status ?? "",
                IsActive
            );
            return new JsonResult(new { success = true });
        }

        public IActionResult OnGetDownloadReport()
        {
            // Always fetch fresh data for all active pumps
            var allActivePumps = _pumpDashboardService.GetPumpsAsync().GetAwaiter().GetResult();
            return _reportExportService.ExportPumpsAsCsv(allActivePumps);
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }
    }
    public class EditUserInputModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string UserType { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
