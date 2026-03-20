using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NLog;

namespace asset_monitoring.Pages
{
    public class DashboardModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); 

        private readonly ApplicationDbContext _db;

        private readonly UserCacheService _userCache;
        private readonly PumpDashboardService _PumpdashboardService;
        private readonly ReportExportService _reportExportService;
        public int TotalPumps { get; private set; }
        public int RunningPumpCount { get; private set; }
        public int OfflinePumpCount { get; private set; }
        public int MaintenancePumpCount { get; private set; }
        public string AvgRuntime { get; private set; } = "0 hrs";

        public List<PumpRow> Pumps { get; private set; } = new();

        [BindProperty]
        public LoginInputModel LoginModel { get; set; } = new();

        public string? LoginMessage { get; private set; }

        public decimal MapCenterLatitude { get; private set; }
        public decimal MapCenterLongitude { get; private set; }
        public int MapZoom { get; private set; }
        public DashboardModel(ApplicationDbContext db, 
            IConfiguration config, UserCacheService userCache, 
            PumpDashboardService pumpdashboardService,
            ReportExportService reportExportService
            )
        {
            _db = db;
            _userCache = userCache;
            _PumpdashboardService = pumpdashboardService;
            MapCenterLatitude = config.GetValue<decimal>("MapSettings:CenterLatitude");
            MapCenterLongitude = config.GetValue<decimal>("MapSettings:CenterLongitude");
            MapZoom = config.GetValue<int>("MapSettings:Zoom");
            _PumpdashboardService = pumpdashboardService;
            _reportExportService = reportExportService;
        }

        public async Task OnGetAsync()
        {
            Logger.Info("Dashboard OnGetAsync started");

            var pumpData = await _PumpdashboardService.GetPumpsAsync();

            Pumps = pumpData.Select(p => new PumpRow
            {
                PumpId = p.PumpId,
                VendorName = p.VendorName,
                Location = p.Location,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Status         = p.Status,
                RunningMinutes = p.RunningMinutes,
                LastUpdated    = p.LastUpdated,
                LastRun        = null,
                OperatorName   = p.OperatorName,
                OperatorMobile = p.OperatorMobile,
                JeName         = p.JeName,
                JeMobile       = p.JeMobile
            }).ToList();

            TotalPumps = Pumps.Count;
            RunningPumpCount = pumpData.Count(p => p.Status == "ON");
            OfflinePumpCount = pumpData.Count(p => p.Status == "OFF");
            MaintenancePumpCount = pumpData.Count(p => p.Status == "MAINTENANCE");

            double totalRunningMinutes = pumpData.Sum(p => p.RunningMinutes);
            AvgRuntime = TotalPumps > 0
                ? $"{(totalRunningMinutes / 60.0 / TotalPumps):0.#} hrs"
                : "0 hrs";

        }


        // ── Simple in-memory login rate limiter (IP → fail count + lockout expiry) ──
        private static readonly Dictionary<string, (int Count, DateTime LockedUntil)> _loginAttempts
            = new();
        private static readonly object _loginLock = new();
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private bool IsLoginRateLimited(string ip)
        {
            lock (_loginLock)
            {
                if (!_loginAttempts.TryGetValue(ip, out var entry)) return false;
                if (entry.LockedUntil > DateTime.UtcNow) return true;
                if (entry.Count >= MaxLoginAttempts)
                {
                    _loginAttempts[ip] = (entry.Count, DateTime.UtcNow.Add(LockoutDuration));
                    return true;
                }
                return false;
            }
        }

        private void RecordFailedLogin(string ip)
        {
            lock (_loginLock)
            {
                _loginAttempts.TryGetValue(ip, out var entry);
                var newCount = entry.Count + 1;
                var lockUntil = newCount >= MaxLoginAttempts
                    ? DateTime.UtcNow.Add(LockoutDuration)
                    : DateTime.MinValue;
                _loginAttempts[ip] = (newCount, lockUntil);
            }
        }

        private void ClearLoginAttempts(string ip)
        {
            lock (_loginLock) { _loginAttempts.Remove(ip); }
        }

        public async Task<IActionResult> OnPostLoginAsync()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Logger.Info("Login attempt for user: {0} from IP: {1}", LoginModel.Username, ip);

            if (string.IsNullOrWhiteSpace(LoginModel.Username) ||
                string.IsNullOrWhiteSpace(LoginModel.Password))
            {
                LoginMessage = "Username and password are required.";
                await OnGetAsync();
                return Page();
            }

            if (IsLoginRateLimited(ip))
            {
                Logger.Warn("Login rate-limited for IP: {0}", ip);
                LoginMessage = "Too many failed attempts. Please try again in 15 minutes.";
                await OnGetAsync();
                return Page();
            }

            if (!_userCache.Users.TryGetValue(LoginModel.Username, out var user))
            {
                RecordFailedLogin(ip);
                Logger.Warn("Login failed (user not found): {0} from IP: {1}", LoginModel.Username, ip);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            // Verify password — support BCrypt hashes and plain-text (auto-upgrades plain-text)
            bool passwordValid = VerifyAndUpgradePassword(user, LoginModel.Password);
            if (!passwordValid)
            {
                RecordFailedLogin(ip);
                Logger.Warn("Login failed (wrong password) for user: {0} from IP: {1}", LoginModel.Username, ip);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            ClearLoginAttempts(ip);
            Logger.Info("Login successful for user: {0}, role={1}", user.Name, user.UserType);

            LoginMessage = $"Welcome {user.Name} ({user.UserType})";
            HttpContext.Session.Clear(); // prevent session fixation
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserType", user.UserType);
            HttpContext.Session.SetString("Mobile", user.MobileNumber);

            if (user.UserType == "ADMIN")
            {
                return RedirectToPage("/Admin");
            }
            else if (user.UserType == "OPERATOR")
            {
                return RedirectToPage("/Operator");
            }
            else if (user.UserType == "JE")
            {
                return RedirectToPage("/JuniorEngineer");
            }

            await OnGetAsync();
            return Page();
        }


        public async Task<JsonResult> OnGetRefreshAsync()
        {
            Logger.Info("Dashboard refresh requested at {0}", DateTime.UtcNow);
            _PumpdashboardService.InvalidateCache();
            await OnGetAsync();
            return new JsonResult(new
            {
                totalPumps = TotalPumps,
                runningNow = RunningPumpCount,
                offlineCount = OfflinePumpCount,
                maintenanceCount = MaintenancePumpCount,
                avgRuntime = AvgRuntime,
                pumps = Pumps
            });
        }
        public record PumpRow
        {
            public string PumpId { get; init; } = "";
            public string? VendorName { get; init; }
            public string? Location { get; init; }
            public decimal? Latitude { get; init; }
            public decimal? Longitude { get; init; }
            public string? Status { get; init; }
            public int RunningMinutes { get; init; }
            public DateTime LastUpdated { get; init; }
            public DateTime? LastRun { get; init; }
            public string? OperatorName { get; init; }
            public string? OperatorMobile { get; init; }
            public string? JeName { get; init; }
            public string? JeMobile { get; init; }
        }
        public async Task<JsonResult> OnGetPumpLogsAsync(int pumpId)
        {
            Logger.Info("OnGetPumpLogsAsync: pumpId={0}", pumpId);
            try
            {
                var logs = await _PumpdashboardService.GetPumpLogsAsync(pumpId);
                return new JsonResult(new { success = true, logs });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnGetPumpLogsAsync failed for pumpId={0}", pumpId);
                return new JsonResult(new { success = false, message = "Failed to load logs" });
            }
        }

        // ── Report downloads — require login ─────────────────────────────────
        public async Task<IActionResult> OnGetDownloadReportAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsCsv(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsXlsx(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportPdfAsync()
        {
            if (!IsLoggedIn) return RedirectToPage("/Index");
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsPdf(pumps);
        }

        // ── Password verification with BCrypt + plain-text migration ──────────
        // Supports: BCrypt hashes (new), plain-text (legacy — auto-upgrades on login)
        private bool VerifyAndUpgradePassword(ActiveUsers user, string plainPassword)
        {
            var stored = user.Password;
            if (string.IsNullOrEmpty(stored)) return false;

            // BCrypt hash starts with $2a$ or $2b$
            if (stored.StartsWith("$2"))
                return BCrypt.Net.BCrypt.Verify(plainPassword, stored);

            // Legacy plain-text — verify then upgrade to hash in DB
            if (stored != plainPassword) return false;

            // Upgrade: hash and save to DB
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<asset_monitoring.Data.ApplicationDbContext>();
                var dbUser = db.BdaUserMasters.Find(user.UserId);
                if (dbUser != null)
                {
                    dbUser.Password = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
                    db.SaveChanges();
                    Logger.Info("VerifyAndUpgradePassword: upgraded password hash for userId={0}", user.UserId);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "VerifyAndUpgradePassword: failed to upgrade hash for userId={0}", user.UserId);
            }

            return true;
        }

        public class LoginInputModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
