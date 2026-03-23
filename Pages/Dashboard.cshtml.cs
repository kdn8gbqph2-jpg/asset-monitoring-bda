using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Pages
{
    public class DashboardModel : AppPageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ApplicationDbContext _db;
        private readonly PumpDashboardService _pumpService;
        private readonly ReportExportService _reportExportService;

        // ── KPI properties ──────────────────────────────────────────────────
        public int TotalPumps { get; private set; }
        public int RunningPumpCount { get; private set; }
        public int OfflinePumpCount { get; private set; }
        public int MaintenancePumpCount { get; private set; }
        public string AvgRuntime { get; private set; } = "0 hrs";

        public List<PumpRow> Pumps { get; private set; } = new();

        [BindProperty]
        public LoginInputModel LoginModel { get; set; } = new();
        public string? LoginMessage { get; private set; }

        // ── Map config ──────────────────────────────────────────────────────
        public decimal MapCenterLatitude { get; private set; }
        public decimal MapCenterLongitude { get; private set; }
        public int MapZoom { get; private set; }

        // ── Constructor ─────────────────────────────────────────────────────
        public DashboardModel(
            ApplicationDbContext db,
            IConfiguration config,
            UserCacheService userCache,
            PumpDashboardService pumpService,
            ReportExportService reportExportService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _pumpService = pumpService ?? throw new ArgumentNullException(nameof(pumpService));
            _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
            // userCache is injected for DI wiring but used only by login (via _userCache field below)
            _userCache = userCache ?? throw new ArgumentNullException(nameof(userCache));

            MapCenterLatitude = config.GetValue<decimal>("MapSettings:CenterLatitude");
            MapCenterLongitude = config.GetValue<decimal>("MapSettings:CenterLongitude");
            MapZoom = config.GetValue<int>("MapSettings:Zoom");
        }

        private readonly UserCacheService _userCache;

        // ═════════════════════════════════════════════════════════════════════
        //  PAGE HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        public async Task OnGetAsync()
        {
            Logger.Info("Dashboard OnGetAsync started");
            try
            {
                var pumpData = await _pumpService.GetPumpsAsync();

                Pumps = pumpData.Select(p => new PumpRow
                {
                    PumpId         = p.PumpId,
                    VendorName     = p.VendorName,
                    Location       = p.Location,
                    Latitude       = p.Latitude,
                    Longitude      = p.Longitude,
                    Status         = p.Status,
                    RunningMinutes = p.RunningMinutes,
                    LastUpdated    = p.LastUpdated,
                    OperatorName   = p.OperatorName,
                    OperatorMobile = p.OperatorMobile,
                    JeName         = p.JeName,
                    JeMobile       = p.JeMobile
                }).ToList();

                TotalPumps           = Pumps.Count;
                RunningPumpCount     = pumpData.Count(p => p.Status == "ON");
                OfflinePumpCount     = pumpData.Count(p => p.Status == "OFF");
                MaintenancePumpCount = pumpData.Count(p => p.Status == "MAINTENANCE");

                double totalRunningMinutes = pumpData.Sum(p => p.RunningMinutes);
                AvgRuntime = TotalPumps > 0
                    ? $"{(totalRunningMinutes / 60.0 / TotalPumps):0.#} hrs"
                    : "0 hrs";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard OnGetAsync failed");
                Pumps = new List<PumpRow>();
            }
        }

        public async Task<JsonResult> OnGetRefreshAsync()
        {
            Logger.Info("Dashboard refresh requested");
            try
            {
                _pumpService.InvalidateCache();
                await OnGetAsync();
                return new JsonResult(new
                {
                    totalPumps       = TotalPumps,
                    runningNow       = RunningPumpCount,
                    offlineCount     = OfflinePumpCount,
                    maintenanceCount = MaintenancePumpCount,
                    avgRuntime       = AvgRuntime,
                    pumps            = Pumps
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard OnGetRefreshAsync failed");
                return new JsonResult(new { error = "Failed to refresh dashboard data" });
            }
        }

        public async Task<JsonResult> OnGetPumpLogsAsync(int pumpId)
        {
            Logger.Info("OnGetPumpLogsAsync: pumpId={0}", pumpId);
            try
            {
                var logs = await _pumpService.GetPumpLogsAsync(pumpId);
                return new JsonResult(new { success = true, logs });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnGetPumpLogsAsync failed for pumpId={0}", pumpId);
                return new JsonResult(new { success = false, message = "Failed to load logs" });
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  REPORT DOWNLOADS
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnGetDownloadReportAsync()
            => await ExportPumps(format: "CSV");

        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()
            => await ExportPumps(format: "XLSX");

        public async Task<IActionResult> OnGetDownloadReportPdfAsync()
            => await ExportPumps(format: "PDF");

        private async Task<IActionResult> ExportPumps(string format)
        {
            try
            {
                var pumps = await _pumpService.GetPumpsAsync();
                Logger.Info("ExportPumps: exporting {0} pumps as {1}", pumps.Count, format);
                return format switch
                {
                    "XLSX" => _reportExportService.ExportPumpsAsXlsx(pumps),
                    "PDF"  => _reportExportService.ExportPumpsAsPdf(pumps),
                    _      => _reportExportService.ExportPumpsAsCsv(pumps),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ExportPumps failed for format={0}", format);
                return StatusCode(500, $"Failed to generate {format} report");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  COMPLAINT SUBMISSION
        // ═════════════════════════════════════════════════════════════════════

        public async Task<JsonResult> OnPostSubmitComplaintAsync([FromBody] ComplaintInputModel input)
        {
            Logger.Info("OnPostSubmitComplaintAsync: pumpId={0}, mobile={1}", input.PumpId, input.ComplainantMobile);
            try
            {
                if (string.IsNullOrWhiteSpace(input.ComplainantName) ||
                    string.IsNullOrWhiteSpace(input.ComplainantMobile) ||
                    string.IsNullOrWhiteSpace(input.ActualStatus))
                {
                    return new JsonResult(new { success = false, message = "Complainant Name, Mobile and Actual Status are required." });
                }

                // Save complaint to DB
                var now = DateTime.UtcNow;
                var complaint = new ComplaintLog
                {
                    PumpId               = int.TryParse(input.PumpId, out var pid) ? pid : 0,
                    Location             = input.Location,
                    DashboardStatus      = input.DashboardStatus,
                    ActualStatus         = input.ActualStatus,
                    OperatorName         = input.OperatorName,
                    OperatorMobile       = input.OperatorMobile,
                    JeName               = input.JeName,
                    JeMobile             = input.JeMobile,
                    ComplainantName      = input.ComplainantName,
                    ComplainantMobile    = input.ComplainantMobile,
                    Status               = "OPEN",
                    RowInsertionDateTime = now,
                    RowUpdationDateTime  = now
                };
                _db.ComplaintLogs.Add(complaint);
                await _db.SaveChangesAsync();
                Logger.Info("Complaint #{0} saved for pumpId={1}", complaint.ComplaintId, input.PumpId);

                // Build WhatsApp URL from app config
                var whatsappUrl = await BuildWhatsAppUrl(input);
                return new JsonResult(new { success = true, whatsappUrl });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostSubmitComplaintAsync failed for pumpId={0}", input.PumpId);
                return new JsonResult(new { success = false, message = "Failed to submit complaint." });
            }
        }

        private async Task<string> BuildWhatsAppUrl(ComplaintInputModel input)
        {
            var configs = await _db.AppConfigs.AsNoTracking().ToListAsync();
            var cfg = configs.ToDictionary(c => c.ConfigKey, c => c.ConfigValue ?? "");

            var sendTo      = cfg.GetValueOrDefault("complaint_whatsapp_send_to", "Fixed Number");
            var fixedNumber = cfg.GetValueOrDefault("complaint_whatsapp_number", "919680111439");
            var template    = cfg.GetValueOrDefault("complaint_message_template", "");

            if (string.IsNullOrWhiteSpace(template))
            {
                template = "*PUMP COMPLAINT*\n\nPump ID: {pump_id}\nVendor: {vendor}\nLocation: {location}\n"
                         + "Dashboard Status: {status}\nActual Status: {actual_status}\n\n"
                         + "Operator: {operator_name} ({operator_mobile})\nJE: {je_name} ({je_mobile})\n\n"
                         + "Complainant: {complainant_name}\nMobile: {complainant_mobile}";
            }

            // Replace placeholders
            var message = template
                .Replace("{pump_id}",           input.PumpId ?? "")
                .Replace("{vendor}",            input.VendorName ?? "")
                .Replace("{location}",          input.Location ?? "")
                .Replace("{status}",            input.DashboardStatus ?? "")
                .Replace("{actual_status}",     input.ActualStatus ?? "")
                .Replace("{operator_name}",     input.OperatorName ?? "")
                .Replace("{operator_mobile}",   input.OperatorMobile ?? "")
                .Replace("{je_name}",           input.JeName ?? "")
                .Replace("{je_mobile}",         input.JeMobile ?? "")
                .Replace("{complainant_name}",  input.ComplainantName ?? "")
                .Replace("{complainant_mobile}", input.ComplainantMobile ?? "");

            // Determine target number
            var targetNumber = ResolveTargetNumber(sendTo, fixedNumber, input.JeMobile);
            var encodedMessage = Uri.EscapeDataString(message.Replace("\\n", "\n"));
            return $"https://wa.me/{targetNumber}?text={encodedMessage}";
        }

        private static string ResolveTargetNumber(string sendTo, string fixedNumber, string? jeMobile)
        {
            // "JE Mobile" or "Both" → prefer JE mobile; fallback to fixed number
            if ((sendTo == "JE Mobile" || sendTo == "Both") && !string.IsNullOrWhiteSpace(jeMobile))
                return jeMobile.StartsWith("91") ? jeMobile : "91" + jeMobile;

            return fixedNumber;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LOGIN (rate-limited, BCrypt + plain-text auto-upgrade)
        // ═════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, (int Count, DateTime LockedUntil)> _loginAttempts = new();
        private static readonly object _loginLock = new();
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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
                Logger.Warn("Login failed (user not found): {0}", LoginModel.Username);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            if (!VerifyAndUpgradePassword(user, LoginModel.Password))
            {
                RecordFailedLogin(ip);
                Logger.Warn("Login failed (wrong password) for user: {0}", LoginModel.Username);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            // Success — set session and redirect by role
            ClearLoginAttempts(ip);
            Logger.Info("Login successful for user: {0}, role={1}", user.Name, user.UserType);

            HttpContext.Session.Clear(); // prevent session fixation
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserType", user.UserType);
            HttpContext.Session.SetString("Mobile", user.MobileNumber);

            return user.UserType switch
            {
                "ADMIN"    => RedirectToPage("/Admin"),
                "OPERATOR" => RedirectToPage("/Operator"),
                "JE"       => RedirectToPage("/JuniorEngineer"),
                _          => RedirectToPage("/Dashboard")
            };
        }

        // ── Rate-limiter helpers ────────────────────────────────────────────

        private static bool IsLoginRateLimited(string ip)
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

        private static void RecordFailedLogin(string ip)
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

        private static void ClearLoginAttempts(string ip)
        {
            lock (_loginLock) { _loginAttempts.Remove(ip); }
        }

        // ── BCrypt verify + plain-text auto-upgrade ─────────────────────────

        private bool VerifyAndUpgradePassword(ActiveUsers user, string plainPassword)
        {
            var stored = user.Password;
            if (string.IsNullOrEmpty(stored)) return false;

            // BCrypt hash starts with $2a$ or $2b$
            if (stored.StartsWith("$2"))
                return BCrypt.Net.BCrypt.Verify(plainPassword, stored);

            // Legacy plain-text — verify then upgrade to hash
            if (stored != plainPassword) return false;

            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dbUser = db.BdaUserMasters.Find(user.UserId);
                if (dbUser != null)
                {
                    dbUser.Password = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
                    db.SaveChanges();
                    Logger.Info("Upgraded password hash for userId={0}", user.UserId);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to upgrade password hash for userId={0}", user.UserId);
            }
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  NESTED DTOs
        // ═════════════════════════════════════════════════════════════════════

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
            public string? OperatorName { get; init; }
            public string? OperatorMobile { get; init; }
            public string? JeName { get; init; }
            public string? JeMobile { get; init; }
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

        public class LoginInputModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
