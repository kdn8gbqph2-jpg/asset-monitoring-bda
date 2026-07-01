using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
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

        public AdminModel(
            ApplicationDbContext context,
            UserCacheService userCache,
            PumpDashboardService pumpDashboardService,
            ReportExportService reportExportService)
        {
            _context = context;
            _userCache = userCache;
            _pumpDashboardService = pumpDashboardService;
            _reportExportService = reportExportService;
        }

        // ── Page properties ─────────────────────────────────────────────────
        public List<BdaUserMaster> Users { get; set; } = new();
        public List<DashboardPumpDto> Pumps { get; set; } = new();
        public List<BdaPumpLocation> Locations { get; set; } = new();
        public PumpRunningSummaryResult RunningSummary { get; set; } = new();
        public List<ComplaintLog> Complaints { get; set; } = new();
        public int OpenComplaintCount { get; set; }
        public Dictionary<string, string> AppSettings { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string? LoggedInUserName { get; set; }

        // ═════════════════════════════════════════════════════════════════════
        //  PAGE LOAD
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnGetAsync(int? summaryYear = null, int? summaryMonth = null)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
            {
                Logger.Warn("OnGetAsync: unauthorized access attempt, user={0}, type={1}", Username, UserType);
                return RedirectToPage("/Index");
            }

            IsAdmin = true;
            LoggedInUserName = Username;
            Logger.Info("OnGetAsync: admin page loaded by user={0}", Username);

            Users = await _context.BdaUserMasters
                .AsNoTracking()
                .Where(u => u.IsActive)
                .ToListAsync();

            Pumps = await _pumpDashboardService.GetAllPumpsAsync(); // incl. deactivated, so admin can re-activate
            RunningSummary = await _pumpDashboardService.GetPumpRunningSummaryAsync(
                year: summaryYear, month: summaryMonth);

            Locations = await _context.BdaPumpLocations
                .AsNoTracking()
                .ToListAsync();

            Complaints = await _context.ComplaintLogs
                .AsNoTracking()
                .OrderByDescending(c => c.RowInsertionDateTime)
                .ToListAsync();
            OpenComplaintCount = Complaints.Count(c => c.Status == "OPEN");

            var configs = await _context.AppConfigs.AsNoTracking().ToListAsync();
            AppSettings = configs.ToDictionary(c => c.ConfigKey, c => c.ConfigValue ?? "");

            Logger.Debug("OnGetAsync: loaded {0} users, {1} pumps, {2} complaints", Users.Count, Pumps.Count, Complaints.Count);
            return Page();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PUMP HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnPostUpdatePumpAsync([FromBody] UpdatePumpRequest req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

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
                return UnauthorizedJson();

            Logger.Info("OnPostAddPumpAsync: vendor={0}, location={1}", req.VendorName, req.LocationName);
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
                return UnauthorizedJson();

            Logger.Info("OnPostDeletePumpAsync: soft-deleting pumpId={0} by admin={1}", id, Username);
            await _pumpDashboardService.DeletePumpAsync(id);
            return RedirectToPage();
        }

        // Activate / deactivate a pump. Deactivated pumps drop off the dashboard
        // but remain in the inventory so they can be re-activated.
        public async Task<IActionResult> OnPostSetActiveAsync(int id, bool active)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            Logger.Info("OnPostSetActiveAsync: pumpId={0} active={1} by admin={2}", id, active, Username);
            await _pumpDashboardService.SetPumpActiveAsync(id, active);
            return RedirectToPage();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  USER HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnPostUpdateUserAsync([FromBody] EditUserInputModel req)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            if (req == null || req.UserId <= 0)
            {
                Logger.Warn("OnPostUpdateUserAsync: invalid input, userId={0}", req?.UserId);
                return new JsonResult(new { success = false, message = "Invalid input" });
            }

            if ((req.Name?.Length ?? 0) > 100 || (req.Username?.Length ?? 0) > 50 ||
                (req.MobileNumber?.Length ?? 0) > 20)
                return new JsonResult(new { success = false, message = "Input exceeds maximum length" });

            Logger.Info("OnPostUpdateUserAsync: userId={0}, userType={1}", req.UserId, req.UserType);
            try
            {
                var user = await _context.BdaUserMasters.FindAsync(req.UserId);
                if (user == null)
                {
                    Logger.Warn("OnPostUpdateUserAsync: userId={0} not found", req.UserId);
                    return new JsonResult(new { success = false, message = "User not found" });
                }

                // Username uniqueness — only check when it's actually changing
                if (!string.IsNullOrWhiteSpace(req.Username) &&
                    !string.Equals(req.Username, user.Username, StringComparison.OrdinalIgnoreCase))
                {
                    var (takenU, suggestionsU) = await CheckUsernameAsync(req.Username.Trim(), user.UserId);
                    if (takenU)
                    {
                        return new JsonResult(new
                        {
                            success     = false,
                            message     = $"Username '{req.Username}' is already taken",
                            suggestions = suggestionsU
                        });
                    }
                }

                user.Name = req.Name;
                if (!string.IsNullOrWhiteSpace(req.Username))
                    user.Username = req.Username;
                if (Enum.TryParse<BdaUserType>(req.UserType, true, out var parsedType))
                    user.UserType = parsedType;
                user.MobileNumber = req.MobileNumber;
                if (!string.IsNullOrWhiteSpace(req.Password))
                    user.Password = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);
                user.IsActive = req.IsActive;
                user.RowUpdationDateTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _userCache.Reload();
                Logger.Info("OnPostUpdateUserAsync: userId={0} updated", req.UserId);
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
                return UnauthorizedJson();

            if (req == null || string.IsNullOrWhiteSpace(req.Name) ||
                string.IsNullOrWhiteSpace(req.MobileNumber) || string.IsNullOrWhiteSpace(req.Username))
            {
                return new JsonResult(new { success = false, message = "Name, Username and Mobile are required" });
            }

            if (req.Name.Length > 100 || req.Username.Length > 50 || req.MobileNumber.Length > 20)
                return new JsonResult(new { success = false, message = "Input exceeds maximum length" });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return new JsonResult(new { success = false, message = "Password must be at least 6 characters" });

            // Username uniqueness check — block duplicates and suggest alternatives
            var (taken, suggestions) = await CheckUsernameAsync(req.Username.Trim());
            if (taken)
            {
                Logger.Info("OnPostAddUserAsync: rejected duplicate username='{0}'", req.Username);
                return new JsonResult(new
                {
                    success     = false,
                    message     = $"Username '{req.Username}' is already taken",
                    suggestions
                });
            }

            Logger.Info("OnPostAddUserAsync: name={0}, userType={1}", req.Name, req.UserType);
            try
            {
                var now = DateTime.UtcNow;
                Enum.TryParse<BdaUserType>(req.UserType, true, out var parsedType);

                var user = new BdaUserMaster
                {
                    Name                 = req.Name,
                    Username             = req.Username,
                    UserType             = parsedType,
                    MobileNumber         = req.MobileNumber,
                    Password             = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
                    IsActive             = true,
                    RowInsertionDateTime = now,
                    RowUpdationDateTime  = now
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
                return UnauthorizedJson();

            Logger.Info("OnPostDeleteUserAsync: deactivating userId={0}", id);
            try
            {
                var user = await _context.BdaUserMasters.FindAsync(id);
                if (user == null)
                    return new JsonResult(new { success = false, message = "User not found" });

                user.IsActive = false;
                user.RowUpdationDateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _userCache.Reload();

                Logger.Info("OnPostDeleteUserAsync: userId={0} deactivated", id);
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
            if (!IsLoggedIn || UserType != "ADMIN")
                return new JsonResult(new { success = false, message = "Unauthorized" });

            try
            {
                var users = await _pumpDashboardService.GetActiveUsersForDrawerAsync();
                return new JsonResult(users);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnGetActiveUsersAsync failed");
                return new JsonResult(new { success = false, message = "Failed to load users" });
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  REPORT DOWNLOADS (pumps + complaints × 3 formats)
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnGetDownloadReportAsync()       => await ExportPumps("CSV");
        public async Task<IActionResult> OnGetDownloadReportXlsxAsync()   => await ExportPumps("XLSX");
        public async Task<IActionResult> OnGetDownloadReportPdfAsync()    => await ExportPumps("PDF");

        public async Task<IActionResult> OnGetDownloadComplaintsCsvAsync()  => await ExportComplaints("CSV");
        public async Task<IActionResult> OnGetDownloadComplaintsXlsxAsync() => await ExportComplaints("XLSX");
        public async Task<IActionResult> OnGetDownloadComplaintsPdfAsync()  => await ExportComplaints("PDF");

        private async Task<IActionResult> ExportPumps(string format)
        {
            if (!IsLoggedIn || UserType != "ADMIN") return RedirectToPage("/Index");
            try
            {
                var pumps = await _pumpDashboardService.GetPumpsAsync();
                Logger.Info("ExportPumps({0}): {1} pumps, admin={2}", format, pumps.Count, Username);
                return format switch
                {
                    "XLSX" => _reportExportService.ExportPumpsAsXlsx(pumps),
                    "PDF"  => _reportExportService.ExportPumpsAsPdf(pumps),
                    _      => _reportExportService.ExportPumpsAsCsv(pumps),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ExportPumps({0}) failed", format);
                return StatusCode(500, $"Failed to generate {format} report");
            }
        }

        // ── Running Summary downloads (CSV / XLSX / PDF) ─────────────────────
        public Task<IActionResult> OnGetDownloadRunningSummaryCsvAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("CSV", summaryYear, summaryMonth);
        public Task<IActionResult> OnGetDownloadRunningSummaryXlsxAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("XLSX", summaryYear, summaryMonth);
        public Task<IActionResult> OnGetDownloadRunningSummaryPdfAsync(int? summaryYear, int? summaryMonth)
            => ExportRunningSummary("PDF", summaryYear, summaryMonth);

        private async Task<IActionResult> ExportRunningSummary(string format, int? year, int? month)
        {
            if (!IsLoggedIn || UserType != "ADMIN") return RedirectToPage("/Index");
            try
            {
                var summary = await _pumpDashboardService.GetPumpRunningSummaryAsync(
                    year: year, month: month);
                Logger.Info("ExportRunningSummary({0}): {1} rows, period={2}-{3}, admin={4}",
                    format, summary.Rows.Count, summary.Year, summary.Month, Username);
                return format switch
                {
                    "XLSX" => _reportExportService.ExportRunningSummaryAsXlsx(summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                    "PDF"  => _reportExportService.ExportRunningSummaryAsPdf (summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                    _      => _reportExportService.ExportRunningSummaryAsCsv (summary.Rows, summary.Year, summary.Month, summary.IsCurrentMonth),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ExportRunningSummary({0}) failed", format);
                return StatusCode(500, $"Failed to generate running summary {format}");
            }
        }

        // ── Running Log download (detailed status-change events for selected pumps) ──
        public Task<IActionResult> OnGetDownloadRunningLogXlsxAsync(string? pumpIds, int? summaryYear, int? summaryMonth)
            => ExportRunningLog(pumpIds, summaryYear, summaryMonth);

        private async Task<IActionResult> ExportRunningLog(string? pumpIds, int? year, int? month)
        {
            if (!IsLoggedIn || UserType != "ADMIN") return RedirectToPage("/Index");
            try
            {
                var ids = ParsePumpIds(pumpIds);
                if (ids.Count == 0) return BadRequest("No pumps selected.");

                // Role-scoped summary; intersect with the requested ids so a user can
                // only export logs for pumps they're allowed to see.
                var summary  = await _pumpDashboardService.GetPumpRunningSummaryAsync(UserId, UserType, year, month);
                var selected = summary.Rows
                    .Where(r => int.TryParse(r.PumpId, out var pid) && ids.Contains(pid))
                    .ToList();
                if (selected.Count == 0) return BadRequest("Selected pumps are not available.");

                var selectedIds = selected.Select(r => int.Parse(r.PumpId)).ToList();
                var events = await _pumpDashboardService.GetPumpRunningLogAsync(selectedIds, summary.Year, summary.Month);
                var byPump = events.GroupBy(e => e.PumpId).ToDictionary(g => g.Key, g => g.ToList());

                Logger.Info("ExportRunningLog: {0} pumps, {1} events, period={2}-{3}, admin={4}",
                    selected.Count, events.Count, summary.Year, summary.Month, Username);
                return _reportExportService.ExportRunningLogAsXlsx(selected, byPump, summary.Year, summary.Month);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ExportRunningLog failed");
                return StatusCode(500, "Failed to generate running log");
            }
        }

        private async Task<IActionResult> ExportComplaints(string format)
        {
            if (!IsLoggedIn || UserType != "ADMIN") return RedirectToPage("/Index");
            try
            {
                var complaints = await _context.ComplaintLogs.AsNoTracking()
                    .OrderByDescending(c => c.RowInsertionDateTime).ToListAsync();
                Logger.Info("ExportComplaints({0}): {1} complaints, admin={2}", format, complaints.Count, Username);
                return format switch
                {
                    "XLSX" => _reportExportService.ExportComplaintsAsXlsx(complaints),
                    "PDF"  => _reportExportService.ExportComplaintsAsPdf(complaints),
                    _      => _reportExportService.ExportComplaintsAsCsv(complaints),
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "ExportComplaints({0}) failed", format);
                return StatusCode(500, $"Failed to generate complaint {format}");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SETTINGS & COMPLAINT STATUS
        // ═════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> OnPostSaveSettingsAsync([FromBody] SaveSettingsInput input)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            Logger.Info("OnPostSaveSettingsAsync by admin={0}", Username);
            try
            {
                var settingsToSave = new Dictionary<string, string>
                {
                    ["complaint_whatsapp_send_to"]  = input.SendTo ?? "Fixed Number",
                    ["complaint_whatsapp_number"]   = input.FixedNumber ?? "",
                    ["complaint_message_template"]  = input.MessageTemplate ?? "",
                    ["complaint_drive_folder"]      = input.DriveFolder ?? ""
                };

                var now = DateTime.UtcNow;
                foreach (var (key, value) in settingsToSave)
                {
                    var existing = await _context.AppConfigs.FirstOrDefaultAsync(c => c.ConfigKey == key);
                    if (existing != null)
                    {
                        existing.ConfigValue = value;
                        existing.RowUpdationDateTime = now;
                    }
                    else
                    {
                        _context.AppConfigs.Add(new AppConfig
                        {
                            ConfigKey = key,
                            ConfigValue = value,
                            RowUpdationDateTime = now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                Logger.Info("Settings saved successfully");
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "OnPostSaveSettingsAsync failed");
                return new JsonResult(new { success = false, message = "Failed to save settings" });
            }
        }

        public IActionResult OnGetRefreshData()
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return RedirectToPage("/Index");

            _pumpDashboardService.InvalidateCache();
            Logger.Info("Cache cleared by admin={0}", Username);
            return RedirectToPage();
        }

        private static readonly string[] ValidComplaintStatuses = { "RESOLVED", "REJECTED" };

        public async Task<IActionResult> OnPostUpdateComplaintStatusAsync([FromBody] UpdateComplaintInput input)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            if (input == null || input.ComplaintId <= 0)
                return new JsonResult(new { success = false, message = "Invalid input" });

            var newStatus = input.Status ?? "RESOLVED";
            if (!ValidComplaintStatuses.Contains(newStatus))
                return new JsonResult(new { success = false, message = "Invalid status value" });

            Logger.Info("UpdateComplaintStatus: #{0} → {1} by {2}", input.ComplaintId, newStatus, Username);
            try
            {
                var complaint = await _context.ComplaintLogs.FindAsync(input.ComplaintId);
                if (complaint == null)
                    return new JsonResult(new { success = false, message = "Complaint not found" });

                complaint.Status = newStatus;
                complaint.Remarks = input.Remarks;
                complaint.RowUpdationDateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdateComplaintStatus failed for #{0}", input.ComplaintId);
                return new JsonResult(new { success = false, message = "Failed to update complaint" });
            }
        }

        public async Task<IActionResult> OnPostDeleteComplaintPhotoAsync([FromBody] DeleteComplaintPhotoInput input)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            if (input == null || input.ComplaintId <= 0)
                return new JsonResult(new { success = false, message = "Invalid input" });

            Logger.Info("DeleteComplaintPhoto: #{0} by {1}", input.ComplaintId, Username);
            try
            {
                var complaint = await _context.ComplaintLogs.FindAsync(input.ComplaintId);
                if (complaint == null)
                    return new JsonResult(new { success = false, message = "Complaint not found" });

                if (string.IsNullOrEmpty(complaint.PhotoPath))
                    return new JsonResult(new { success = false, message = "No photo attached" });

                DeletePhysicalPhoto(complaint.PhotoPath);

                complaint.PhotoPath = null;
                complaint.RowUpdationDateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Logger.Info("DeleteComplaintPhoto: photo removed for #{0}", input.ComplaintId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "DeleteComplaintPhoto failed for #{0}", input.ComplaintId);
                return new JsonResult(new { success = false, message = "Failed to delete photo" });
            }
        }

        /// <summary>
        /// Deletes the on-disk file for a stored complaint photo. The path is
        /// from our own DB, but we still confine deletion to the uploads folder
        /// so a malformed value can never reach outside wwwroot/uploads.
        /// A missing file is treated as success (the DB row is what matters).
        /// </summary>
        private void DeletePhysicalPhoto(string relativePath)
        {
            try
            {
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var uploadsRoot = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads", "complaints"));
                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(env.WebRootPath, normalized));

                if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn("DeletePhysicalPhoto: path '{0}' resolved outside uploads root — skipping file delete", relativePath);
                    return;
                }

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    Logger.Info("DeletePhysicalPhoto: deleted {0}", relativePath);
                }
                else
                {
                    Logger.Warn("DeletePhysicalPhoto: file not found on disk: {0}", relativePath);
                }
            }
            catch (Exception ex)
            {
                // Don't fail the whole operation if the file can't be removed —
                // the DB reference is cleared regardless.
                Logger.Warn(ex, "DeletePhysicalPhoto: failed to delete file {0}", relativePath);
            }
        }

        public IActionResult OnPostLogout()
        {
            Logger.Info("Admin {0} logged out", Username);
            return LogoutAndRedirect();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  USERNAME AVAILABILITY
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Live-check whether a username is available. Returns availability and,
        /// if taken, a few suggested alternatives. Used by the Add/Edit User drawer.
        /// </summary>
        public async Task<IActionResult> OnGetCheckUsernameAsync(string username, int? excludeUserId = null)
        {
            if (!IsLoggedIn || UserType != "ADMIN")
                return UnauthorizedJson();

            if (string.IsNullOrWhiteSpace(username))
                return new JsonResult(new { available = false, suggestions = Array.Empty<string>() });

            var (taken, suggestions) = await CheckUsernameAsync(username.Trim(), excludeUserId);
            return new JsonResult(new { available = !taken, suggestions });
        }

        /// <summary>
        /// Returns (taken, suggestions). Uniqueness is checked across ALL users
        /// (active + inactive) so a soft-deleted username cannot be silently
        /// resurrected on a new row — admins can reactivate the original instead.
        /// Suggestions try numeric increments: if the name ends in digits they
        /// bump the trailing number; otherwise they append 1, 2, 3…
        /// </summary>
        private async Task<(bool taken, List<string> suggestions)> CheckUsernameAsync(
            string username, int? excludeUserId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, new List<string>());

            var query = _context.BdaUserMasters
                .Where(u => u.Username != null && u.Username == username);
            if (excludeUserId.HasValue)
                query = query.Where(u => u.UserId != excludeUserId.Value);

            var isTaken = await query.AnyAsync();
            if (!isTaken)
                return (false, new List<string>());

            // Build candidate list, then filter against DB in one round-trip.
            // Split trailing digits so "admin_1" suggests "admin_2", not "admin_11".
            string baseName = username;
            int startFrom   = 1;
            var match = System.Text.RegularExpressions.Regex.Match(username, @"^(.*?)(\d+)$");
            if (match.Success && int.TryParse(match.Groups[2].Value, out var parsed))
            {
                baseName  = match.Groups[1].Value;
                startFrom = parsed + 1;
            }

            var candidates = new List<string>();
            for (int i = startFrom; i < startFrom + 20 && candidates.Count < 15; i++)
            {
                var cand = baseName + i;
                if (cand.Length <= 50)
                    candidates.Add(cand);
            }

            var excludeId = excludeUserId ?? 0;
            var taken = await _context.BdaUserMasters
                .Where(u => u.Username != null
                         && candidates.Contains(u.Username)
                         && u.UserId != excludeId)
                .Select(u => u.Username!)
                .ToListAsync();

            var takenSet = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
            var available = candidates
                .Where(c => !takenSet.Contains(c))
                .Take(3)
                .ToList();

            return (true, available);
        }

        // ── Helper: standard 403 response ───────────────────────────────────
        private static JsonResult UnauthorizedJson()
            => new(new { success = false, message = "Unauthorized" }) { StatusCode = 403 };
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INPUT MODELS
    // ═════════════════════════════════════════════════════════════════════════

    public class UpdateComplaintInput
    {
        public int ComplaintId { get; set; }
        public string? Status { get; set; }
        public string? Remarks { get; set; }
    }

    public class DeleteComplaintPhotoInput
    {
        public int ComplaintId { get; set; }
    }

    public class SaveSettingsInput
    {
        public string? SendTo { get; set; }
        public string? FixedNumber { get; set; }
        public string? MessageTemplate { get; set; }
        public string? DriveFolder { get; set; }
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
