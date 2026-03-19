using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Pages
{
    public class AdminModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
            LoggedInUserName = Username;

            if (!IsAdmin)
            {
                Logger.Warn("OnGetAsync: non-admin access attempt by user={0}, userType={1}", Username, userType);
                return Page();
            }

            Logger.Info("OnGetAsync: admin page loaded by user={0}", Username);

            Users = await _context.BdaUserMasters
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();

            // No username filter for admin: fetch all active pumps with mobile number
            Pumps = await _pumpDashboardService.GetPumpsAsync();

            Locations = await _context.BdaPumpLocations
                .AsNoTracking()
                .ToListAsync();

            Logger.Debug("OnGetAsync: loaded {0} users, {1} pumps for admin", Users.Count, Pumps.Count);
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

        [BindProperty]
        public string? Latitude { get; set; }
        [BindProperty]
        public string? Longitude { get; set; }

        [BindProperty]
        public EditUserInputModel EditUser { get; set; } = new();

        public async Task<IActionResult> OnPostUpdateUserAsync()
        {
            if (EditUser == null || EditUser.UserId <= 0)
            {
                Logger.Warn("OnPostUpdateUserAsync: invalid input received, userId={0}", EditUser?.UserId);
                return new JsonResult(new { success = false, message = "Invalid input" });
            }

            Logger.Info("OnPostUpdateUserAsync: updating userId={0}, name={1}, userType={2} by admin={3}",
                EditUser.UserId, EditUser.Name, EditUser.UserType, Username);

            try
            {
                var rows = await _context.Database.ExecuteSqlRawAsync(
                    "CALL sp_update_user_master({0},{1},{2},{3},{4},{5})",
                    EditUser.UserId,
                    EditUser.Name,
                    EditUser.UserType,   // pass string enum
                    EditUser.MobileNumber,
                    string.IsNullOrWhiteSpace(EditUser.Password)
                        ? String.Empty
                        : EditUser.Password,   // hash before this in prod
                    EditUser.IsActive ? 1 : 0
                );

                if (rows <= 0)
                {
                    Logger.Warn("OnPostUpdateUserAsync: sp_update_user_master affected 0 rows for userId={0}", EditUser.UserId);
                    return new JsonResult(new { success = false });
                }

                _userCache.Reload();
                Logger.Info("OnPostUpdateUserAsync: userId={0} updated successfully, user cache reloaded", EditUser.UserId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostUpdateUserAsync failed for userId={0}", EditUser.UserId);
                return new JsonResult(new { success = false, message = "An error occurred" });
            }
        }



        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            Logger.Info("OnPostDeletePumpAsync: soft-deleting pumpId={0} by admin={1}", id, Username);
            await _pumpDashboardService.DeletePumpAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdatePumpAsync()
        {
            Logger.Info("OnPostUpdatePumpAsync: updating pumpId={0} by admin={1}", PumpId, Username);

            try
            {
                await _pumpDashboardService.UpdatePumpDetailsAsync(
                    PumpId,
                    VendorName ?? "",
                    Category,
                    LocationName ?? "",
                    Status ?? "",
                    Latitude ?? "",
                    Longitude ?? "",
                    IsActive
                );
                Logger.Info("OnPostUpdatePumpAsync: pumpId={0} updated successfully", PumpId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostUpdatePumpAsync failed for pumpId={0}", PumpId);
                return new JsonResult(new { success = false, message = "Update failed" });
            }
        }

        public IActionResult OnGetDownloadReport()
        {
            Logger.Info("OnGetDownloadReport: report download requested by admin={0}", Username);
            var allActivePumps = _pumpDashboardService.GetPumpsAsync().GetAwaiter().GetResult();
            Logger.Info("OnGetDownloadReport: exporting {0} pumps to CSV", allActivePumps.Count);
            return _reportExportService.ExportPumpsAsCsv(allActivePumps);
        }

        public IActionResult OnPostLogout()
        {
            Logger.Info("OnPostLogout: admin={0} logged out", Username);
            return LogoutAndRedirect();
        }

    }


    public class EditUserInputModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string UserType { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string Password { get; set; } = "";
        public bool IsActive { get; set; }
    }


}
