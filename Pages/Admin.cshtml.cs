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
        public List<PumpRunningSummaryDto> RunningSummary { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string? LoggedInUserName { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!IsLoggedIn)
            {
                Logger.Warn("OnGetAsync: unauthenticated access attempt to admin page");
                return RedirectToPage("/Index");
            }

            var userType = HttpContext.Session.GetString("UserType");
            IsAdmin = userType == "ADMIN";
            LoggedInUserName = Username;

            if (!IsAdmin)
            {
                Logger.Warn("OnGetAsync: non-admin access attempt by user={0}, userType={1}", Username, userType);
                return RedirectToPage("/Index");
            }

            Logger.Info("OnGetAsync: admin page loaded by user={0}", Username);

            Users = await _context.BdaUserMasters
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();

            // No username filter for admin: fetch all active pumps with mobile number
            Pumps = await _pumpDashboardService.GetPumpsAsync();
            RunningSummary = await _pumpDashboardService.GetPumpRunningSummaryAsync();

            Locations = await _context.BdaPumpLocations
                .AsNoTracking()
                .ToListAsync();

            Logger.Debug("OnGetAsync: loaded {0} users, {1} pumps for admin", Users.Count, Pumps.Count);
            return Page();
        }

        // ── Pump handlers ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostUpdatePumpAsync([FromBody] UpdatePumpRequest req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostUpdatePumpAsync: pumpId={0} by admin={1}", req.PumpId, Username);
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

        public async Task<IActionResult> OnPostAddPumpAsync([FromBody] AddPumpRequest req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostAddPumpAsync: vendor={0}, location={1} by admin={2}", req.VendorName, req.LocationName, Username);
            try
            {
                req.UpdatedBy = Username;
                var newId = await _pumpDashboardService.AddPumpAsync(req);
                Logger.Info("OnPostAddPumpAsync: created pumpId={0}", newId);
                return new JsonResult(new { success = true, pumpId = newId });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostAddPumpAsync failed for vendor={0}", req.VendorName);
                return new JsonResult(new { success = false, message = "Failed to add pump" });
            }
        }

        public async Task<IActionResult> OnPostDeletePumpAsync(int id)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostDeletePumpAsync: soft-deleting pumpId={0} by admin={1}", id, Username);
            await _pumpDashboardService.DeletePumpAsync(id);
            return RedirectToPage();
        }

        // ── User handlers ────────────────────────────────────────────────────

        public async Task<IActionResult> OnPostUpdateUserAsync([FromBody] EditUserInputModel req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            if (req == null || req.UserId <= 0)
            {
                Logger.Warn("OnPostUpdateUserAsync: invalid input, userId={0}", req?.UserId);
                return new JsonResult(new { success = false, message = "Invalid input" });
            }

            Logger.Info("OnPostUpdateUserAsync: userId={0}, userType={1} by admin={2}", req.UserId, req.UserType, Username);

            try
            {
                var user = await _context.BdaUserMasters.FindAsync(req.UserId);
                if (user == null)
                {
                    Logger.Warn("OnPostUpdateUserAsync: userId={0} not found", req.UserId);
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                user.Name = req.Name;
                if (!string.IsNullOrWhiteSpace(req.Username))
                    user.Username = req.Username;
                if (Enum.TryParse<BdaUserType>(req.UserType, true, out var parsedType))
                    user.UserType = parsedType;
                user.MobileNumber = req.MobileNumber;
                if (!string.IsNullOrWhiteSpace(req.Password))
                    user.Password = req.Password; // Sprint 4: hash this
                user.IsActive = req.IsActive;
                user.RowUpdationDateTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _userCache.Reload();

                Logger.Info("OnPostUpdateUserAsync: userId={0} updated, cache reloaded", req.UserId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostUpdateUserAsync failed for userId={0}", req.UserId);
                return new JsonResult(new { success = false, message = "An error occurred" });
            }
        }

        public async Task<IActionResult> OnPostAddUserAsync([FromBody] EditUserInputModel req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            if (req == null || string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.MobileNumber) || string.IsNullOrWhiteSpace(req.Username))
            {
                Logger.Warn("OnPostAddUserAsync: missing required fields");
                return new JsonResult(new { success = false, message = "Name, Username and Mobile are required" });
            }

            Logger.Info("OnPostAddUserAsync: name={0}, userType={1} by admin={2}", req.Name, req.UserType, Username);

            try
            {
                var now = DateTime.UtcNow;
                Enum.TryParse<BdaUserType>(req.UserType, true, out var parsedType);

                var user = new BdaUserMaster
                {
                    Name = req.Name,
                    Username = req.Username,
                    UserType = parsedType,
                    MobileNumber = req.MobileNumber,
                    Password = req.Password, // Sprint 4: hash this
                    IsActive = true,
                    RowInsertionDateTime = now,
                    RowUpdationDateTime = now
                };

                _context.BdaUserMasters.Add(user);
                await _context.SaveChangesAsync();
                _userCache.Reload();

                Logger.Info("OnPostAddUserAsync: created userId={0}", user.UserId);
                return new JsonResult(new { success = true, userId = user.UserId });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostAddUserAsync failed for name={0}", req.Name);
                return new JsonResult(new { success = false, message = "Failed to add user" });
            }
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(int id)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };

            Logger.Info("OnPostDeleteUserAsync: deactivating userId={0} by admin={1}", id, Username);
            try
            {
                var user = await _context.BdaUserMasters.FindAsync(id);
                if (user == null)
                {
                    Logger.Warn("OnPostDeleteUserAsync: userId={0} not found", id);
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                user.IsActive = false;
                user.RowUpdationDateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _userCache.Reload();

                Logger.Info("OnPostDeleteUserAsync: userId={0} deactivated, cache reloaded", id);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostDeleteUserAsync failed for userId={0}", id);
                return new JsonResult(new { success = false, message = "An error occurred" });
            }
        }

        public async Task<JsonResult> OnGetActiveUsersAsync()
        {
            var users = await _pumpDashboardService.GetActiveUsersForDrawerAsync();
            return new JsonResult(users);
        }

        public async Task<IActionResult> OnGetDownloadReportAsync()
        {
            var pumps = await _pumpDashboardService.GetPumpsAsync();
            Logger.Info("OnGetDownloadReport: exporting {0} pumps as CSV, admin={1}", pumps.Count, Username);
            return _reportExportService.ExportPumpsAsCsv(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()
        {
            var pumps = await _pumpDashboardService.GetPumpsAsync();
            Logger.Info("OnGetDownloadReportXlsx: exporting {0} pumps as XLSX, admin={1}", pumps.Count, Username);
            return _reportExportService.ExportPumpsAsXlsx(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportPdfAsync()
        {
            var pumps = await _pumpDashboardService.GetPumpsAsync();
            Logger.Info("OnGetDownloadReportPdf: exporting {0} pumps as PDF, admin={1}", pumps.Count, Username);
            return _reportExportService.ExportPumpsAsPdf(pumps);
        }

        public IActionResult OnGetRefreshData()
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return RedirectToPage("/Index");

            _pumpDashboardService.InvalidateCache();
            Logger.Info("OnGetRefreshData: cache cleared by admin={0}", Username);
            return RedirectToPage();
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
        public string Username { get; set; } = "";
        public string UserType { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string Password { get; set; } = "";
        public bool IsActive { get; set; }
    }


}
