using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsCsv(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()
        {
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsXlsx(pumps);
        }

        public async Task<IActionResult> OnGetDownloadReportPdfAsync()
        {
            var pumps = await _PumpdashboardService.GetPumpsAsync();
            return _reportExportService.ExportPumpsAsPdf(pumps);
        }

        // ── Complaint submission ──────────────────────────────────────────────
        public async Task<JsonResult> OnPostSubmitComplaintAsync([FromBody] ComplaintInputModel input)
        {
            Logger.Info("OnPostSubmitComplaintAsync: pumpId={0}, mobile={1}", input.PumpId, input.ComplainantMobile);
            try
            {
                if (string.IsNullOrWhiteSpace(input.ComplainantMobile) || string.IsNullOrWhiteSpace(input.ActualStatus))
                {
                    return new JsonResult(new { success = false, message = "Actual Status and Mobile are required." });
                }

                var now = DateTime.UtcNow;
                var complaint = new ComplaintLog
                {
                    PumpId              = int.TryParse(input.PumpId, out var pid) ? pid : 0,
                    Location            = input.Location,
                    DashboardStatus     = input.DashboardStatus,
                    ActualStatus        = input.ActualStatus,
                    OperatorName        = input.OperatorName,
                    OperatorMobile      = input.OperatorMobile,
                    JeName              = input.JeName,
                    JeMobile            = input.JeMobile,
                    ComplainantName     = input.ComplainantName,
                    ComplainantMobile   = input.ComplainantMobile,
                    Status              = "OPEN",
                    RowInsertionDateTime = now,
                    RowUpdationDateTime  = now
                };

                _db.ComplaintLogs.Add(complaint);
                await _db.SaveChangesAsync();

                Logger.Info("OnPostSubmitComplaintAsync: complaint #{0} saved for pumpId={1}", complaint.ComplaintId, input.PumpId);

                // Build WhatsApp URL from app config
                var configs = await _db.AppConfigs.AsNoTracking().ToListAsync();
                var configDict = configs.ToDictionary(c => c.ConfigKey, c => c.ConfigValue ?? "");

                configDict.TryGetValue("complaint_whatsapp_send_to", out var sendTo);
                configDict.TryGetValue("complaint_whatsapp_number", out var fixedNumber);
                configDict.TryGetValue("complaint_message_template", out var template);

                if (string.IsNullOrWhiteSpace(template))
                {
                    template = "*PUMP COMPLAINT*\n\nPump ID: {pump_id}\nVendor: {vendor}\nLocation: {location}\nDashboard Status: {status}\nActual Status: {actual_status}\n\nOperator: {operator_name} ({operator_mobile})\nJE: {je_name} ({je_mobile})\n\nComplainant: {complainant_name}\nMobile: {complainant_mobile}";
                }

                // Replace placeholders
                var message = template
                    .Replace("{pump_id}", input.PumpId ?? "")
                    .Replace("{vendor}", input.VendorName ?? "")
                    .Replace("{location}", input.Location ?? "")
                    .Replace("{status}", input.DashboardStatus ?? "")
                    .Replace("{actual_status}", input.ActualStatus ?? "")
                    .Replace("{operator_name}", input.OperatorName ?? "")
                    .Replace("{operator_mobile}", input.OperatorMobile ?? "")
                    .Replace("{je_name}", input.JeName ?? "")
                    .Replace("{je_mobile}", input.JeMobile ?? "")
                    .Replace("{complainant_name}", input.ComplainantName ?? "")
                    .Replace("{complainant_mobile}", input.ComplainantMobile ?? "");

                // Determine target number(s) — build URL for the first target
                // sendTo: "JE Mobile" / "Fixed Number" / "Both"
                string targetNumber = fixedNumber ?? "919680111439";
                if (sendTo == "JE Mobile" && !string.IsNullOrWhiteSpace(input.JeMobile))
                {
                    targetNumber = input.JeMobile.StartsWith("91") ? input.JeMobile : "91" + input.JeMobile;
                }
                else if (sendTo == "Both" && !string.IsNullOrWhiteSpace(input.JeMobile))
                {
                    // Primary = JE mobile; the user can manually send to fixed number too
                    targetNumber = input.JeMobile.StartsWith("91") ? input.JeMobile : "91" + input.JeMobile;
                }

                var encodedMessage = Uri.EscapeDataString(message.Replace("\\n", "\n"));
                var whatsappUrl = $"https://wa.me/{targetNumber}?text={encodedMessage}";

                return new JsonResult(new { success = true, whatsappUrl });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostSubmitComplaintAsync failed for pumpId={0}", input.PumpId);
                return new JsonResult(new { success = false, message = "Failed to submit complaint." });
            }
        }

        public class ComplaintInputModel
        {
            public string? PumpId { get; set; }
            public string? VendorName { get; set; }
            public string? Location { get; set; }
            public string? DashboardStatus { get; set; }
            public string? ActualStatus { get; set; }
            public string? OperatorName { get; set; }
            public string? OperatorMobile { get; set; }
            public string? JeName { get; set; }
            public string? JeMobile { get; set; }
            public string? ComplainantName { get; set; }
            public string? ComplainantMobile { get; set; }
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
