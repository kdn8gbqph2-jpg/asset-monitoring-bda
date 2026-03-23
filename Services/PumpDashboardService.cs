using asset_monitoring.Data;
using asset_monitoring.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace asset_monitoring.Services
{
    // ── Request DTOs used by handlers (sent as JSON from JS) ─────────────────
    public class UpdatePumpRequest
    {
        public int PumpId { get; set; }
        public string VendorName { get; set; } = "";
        public string? Category { get; set; }
        public string LocationName { get; set; } = "";
        public string Status { get; set; } = "OFF";
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Remarks { get; set; }          // operator comment
        public string? UpdatedBy { get; set; }        // operator mobile (set by handler)
        public string? OperatorMobile { get; set; }   // assigned operator's mobile
        public string? JeMobile { get; set; }          // assigned JE's mobile
    }

    public class AddPumpRequest
    {
        public string VendorName { get; set; } = "";
        public string? Category { get; set; }
        public string LocationName { get; set; } = "";
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? OperatorMobile { get; set; }
        public string? JeMobile { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class AssignOperatorRequest
    {
        public int PumpId { get; set; }
        public string OperatorMobile { get; set; } = "";
        public string? UpdatedBy { get; set; }
    }

    public class PumpLogDto
    {
        public int LogId { get; set; }
        public string OldStatus { get; set; } = "-";
        public string NewStatus { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Remarks { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class PumpDashboardService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly DailySummaryService _dailySummary;

        private const string ADMIN_CACHE_KEY = "PUMP_DASHBOARD_ADMIN";

        public PumpDashboardService(ApplicationDbContext db, IMemoryCache cache, DailySummaryService dailySummary)
        {
            _db = db;
            _cache = cache;
            _dailySummary = dailySummary;
        }

        // ── Force-invalidate the ADMIN pump cache ─────────────────────────────
        public void InvalidateCache()
        {
            _cache.Remove(ADMIN_CACHE_KEY);
            Logger.Info("InvalidateCache: ADMIN pump cache cleared");
        }

        // ── Public query entry-point ──────────────────────────────────────────
        public async Task<List<DashboardPumpDto>> GetPumpsAsync(
            int? userId = null,
            string? user_type = "ADMIN")
        {
            if (user_type == "ADMIN")
            {
                if (_cache.TryGetValue(ADMIN_CACHE_KEY, out List<DashboardPumpDto>? cached) && cached != null)
                {
                    Logger.Debug("GetPumpsAsync: cache hit — {0} pumps for ADMIN", cached.Count);
                    return cached;
                }

                Logger.Debug("GetPumpsAsync: cache miss for ADMIN, querying DB");
                var pumps = await FetchFromDatabaseAsync(null, "ADMIN");

                _cache.Set(ADMIN_CACHE_KEY, pumps, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    SlidingExpiration               = TimeSpan.FromMinutes(2)
                });

                Logger.Info("GetPumpsAsync: cached {0} pumps for ADMIN", pumps.Count);
                return pumps;
            }

            Logger.Debug("GetPumpsAsync: fresh query for userId={0}, userType={1}", userId, user_type);
            return await FetchFromDatabaseAsync(userId, user_type);
        }

        // ── Core EF Core LINQ query (replaces sp_get_pump_dashboard_data) ─────
        private async Task<List<DashboardPumpDto>> FetchFromDatabaseAsync(
            int? userId, string? user_type)
        {
            try
            {
                // For non-admin: look up user's mobile and route to the correct filter column
                // OPERATOR → filter by entry.operator_mobile
                // JE       → filter by entry.je_mobile
                string? operatorMobile = null;
                string? jeMobile = null;
                if (user_type != "ADMIN" && userId.HasValue)
                {
                    var userMobile = await _db.BdaUserMasters
                        .Where(u => u.UserId == userId.Value && u.IsActive)
                        .Select(u => u.MobileNumber)
                        .FirstOrDefaultAsync();

                    if (user_type == "JE")
                        jeMobile = userMobile;
                    else
                        operatorMobile = userMobile;

                    Logger.Debug("FetchFromDatabaseAsync: mobile={0} for userId={1}, userType={2}",
                        userMobile, userId, user_type);
                }

                // Build a mobile→name lookup from user master (small table — cheap)
                var userMap = (await _db.BdaUserMasters
                    .Where(u => u.IsActive && u.MobileNumber != null)
                    .AsNoTracking()
                    .Select(u => new { u.MobileNumber, u.Name })
                    .ToListAsync())
                    .ToDictionary(u => u.MobileNumber!, u => u.Name ?? "");

                // Join pump master → location (left) → status entry (left)
                var rawData = await (
                    from pump in _db.BdaPumpMasters
                    where pump.IsActive
                    join loc   in _db.BdaPumpLocations   on pump.PumpId equals loc.PumpId   into locGroup
                    from loc   in locGroup.DefaultIfEmpty()
                    join entry in _db.PumpStatusEntries  on pump.PumpId equals entry.PumpId into entryGroup
                    from entry in entryGroup.DefaultIfEmpty()
                    where (operatorMobile == null && jeMobile == null)
                       || (operatorMobile != null && entry != null && entry.OperatorMobile == operatorMobile)
                       || (jeMobile       != null && entry != null && entry.JeMobile       == jeMobile)
                    select new
                    {
                        pump.PumpId,
                        pump.VendorName,
                        LocationName     = loc   != null ? loc.LocationName          : null,
                        Latitude         = loc   != null ? loc.Latitude              : (decimal?)null,
                        Longitude        = loc   != null ? loc.Longitude             : (decimal?)null,
                        EntryStatus      = entry != null ? (PumpStatus?)entry.Status : null,
                        CurrentStartTime = entry != null ? entry.CurrentStartTime    : (DateTime?)null,
                        LastUpdated      = entry != null ? entry.RowUpdationDateTime : pump.RowUpdationDateTime,
                        OperatorMobile   = entry != null ? entry.OperatorMobile      : null,
                        JeMobile         = entry != null ? entry.JeMobile            : null
                    }
                ).AsNoTracking().ToListAsync();

                // Compute running minutes in memory (can't translate DateDiff to EF easily)
                var pumps = rawData.Select(r => new DashboardPumpDto
                {
                    PumpId         = r.PumpId.ToString(),
                    VendorName     = r.VendorName,
                    Location       = r.LocationName,
                    Latitude       = r.Latitude,
                    Longitude      = r.Longitude,
                    Status         = r.EntryStatus switch
                    {
                        PumpStatus.On          => "ON",
                        PumpStatus.Maintenance => "MAINTENANCE",
                        _                      => "OFF"
                    },
                    RunningMinutes  = r.EntryStatus == PumpStatus.On && r.CurrentStartTime.HasValue
                        ? (int)(DateTime.UtcNow - r.CurrentStartTime.Value).TotalMinutes
                        : 0,
                    LastUpdated     = r.LastUpdated,
                    OperatorMobile  = r.OperatorMobile,
                    OperatorName    = r.OperatorMobile != null && userMap.TryGetValue(r.OperatorMobile, out var opN) ? opN : null,
                    JeMobile        = r.JeMobile,
                    JeName          = r.JeMobile != null && userMap.TryGetValue(r.JeMobile, out var jeN) ? jeN : null,
                }).ToList();

                Logger.Debug("FetchFromDatabaseAsync: {0} pumps for userId={1}, userType={2}",
                    pumps.Count, userId, user_type);

                return pumps;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "FetchFromDatabaseAsync failed for userId={0}, userType={1}", userId, user_type);
                throw;
            }
        }

        // ── Soft-delete pump + clear cache ────────────────────────────────────
        public async Task<bool> DeletePumpAsync(int pumpId)
        {
            Logger.Info("DeletePumpAsync: pumpId={0}", pumpId);

            var pump = await _db.BdaPumpMasters.FindAsync(pumpId);
            if (pump == null)
            {
                Logger.Warn("DeletePumpAsync: pumpId={0} not found", pumpId);
                return false;
            }

            pump.IsActive            = false;
            pump.RowUpdationDateTime = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _cache.Remove(ADMIN_CACHE_KEY);
            Logger.Info("DeletePumpAsync: pumpId={0} deactivated, cache cleared", pumpId);
            return true;
        }

        // ── Update pump details (replaces sp_update_pump_details) ─────────────
        public async Task UpdatePumpDetailsAsync(UpdatePumpRequest req)
        {
            Logger.Info("UpdatePumpDetailsAsync: pumpId={0}, status={1}, isActive={2}",
                req.PumpId, req.Status, req.IsActive);
            try
            {
                var now = DateTime.UtcNow;

                // 1. BdaPumpMaster
                var pump = await _db.BdaPumpMasters.FindAsync(req.PumpId);
                if (pump == null)
                {
                    Logger.Warn("UpdatePumpDetailsAsync: pumpId={0} not found", req.PumpId);
                    return;
                }
                pump.VendorName          = req.VendorName;
                pump.Category            = req.Category;
                pump.IsActive            = req.IsActive;
                pump.UpdatedBy           = req.UpdatedBy;
                pump.RowUpdationDateTime = now;

                // 2. BdaPumpLocation (upsert)
                var location = await _db.BdaPumpLocations.FindAsync(req.PumpId);
                if (location == null)
                {
                    _db.BdaPumpLocations.Add(new BdaPumpLocation
                    {
                        PumpId               = req.PumpId,
                        LocationName         = req.LocationName,
                        Latitude             = req.Latitude,
                        Longitude            = req.Longitude,
                        RowActionCount       = 1,
                        RowInsertionDateTime = now,
                        RowUpdationDateTime  = now
                    });
                }
                else
                {
                    location.LocationName        = req.LocationName;
                    location.Latitude            = req.Latitude;
                    location.Longitude           = req.Longitude;
                    location.RowUpdationDateTime = now;
                }

                // 3. PumpStatusEntry (upsert) — write log entry when status changes
                var newStatus = req.Status.ToUpperInvariant() switch
                {
                    "ON"          => PumpStatus.On,
                    "MAINTENANCE" => PumpStatus.Maintenance,
                    _             => PumpStatus.Off
                };

                var entry = await _db.PumpStatusEntries.FindAsync(req.PumpId);
                if (entry == null)
                {
                    _db.PumpStatusEntries.Add(new PumpStatusEntry
                    {
                        PumpId               = req.PumpId,
                        Status               = newStatus,
                        Remarks              = req.Remarks,
                        UpdatedBy            = req.UpdatedBy,
                        OperatorMobile       = string.IsNullOrEmpty(req.OperatorMobile) ? null : req.OperatorMobile,
                        JeMobile             = string.IsNullOrEmpty(req.JeMobile) ? null : req.JeMobile,
                        CurrentStartTime     = newStatus == PumpStatus.On ? now : null,
                        RowActionCount       = 1,
                        RowInsertionDateTime = now,
                        RowUpdationDateTime  = now
                    });
                }
                else
                {
                    var oldStatus = entry.Status;

                    // Write audit log whenever the status actually changes
                    if (oldStatus != newStatus)
                    {
                        _db.PumpStatusLogs.Add(new PumpStatusLog
                        {
                            PumpId               = req.PumpId,
                            OldStatus            = oldStatus,
                            NewStatus            = newStatus,
                            StartTime            = entry.CurrentStartTime,
                            EndTime              = newStatus != PumpStatus.On ? now : null,
                            Remarks              = req.Remarks,
                            UpdatedBy            = req.UpdatedBy,
                            RowInsertionDateTime = now,
                            RowUpdationDateTime  = now
                        });

                        Logger.Info("UpdatePumpDetailsAsync: pumpId={0} status {1}→{2}",
                            req.PumpId, oldStatus, newStatus);
                    }

                    // Track running time: set start when turning ON, set end when turning OFF
                    if (newStatus == PumpStatus.On && oldStatus != PumpStatus.On)
                        entry.CurrentStartTime = now;
                    else if (newStatus != PumpStatus.On && oldStatus == PumpStatus.On)
                    {
                        entry.CurrentEndTime = now;
                        entry.LastRunTime    = now;
                    }

                    entry.Status              = newStatus;
                    entry.Remarks             = req.Remarks;
                    entry.UpdatedBy           = req.UpdatedBy;
                    if (req.OperatorMobile != null)
                        entry.OperatorMobile  = string.IsNullOrEmpty(req.OperatorMobile) ? null : req.OperatorMobile;
                    if (req.JeMobile != null)
                        entry.JeMobile        = string.IsNullOrEmpty(req.JeMobile) ? null : req.JeMobile;
                    entry.RowActionCount     += 1;
                    entry.RowUpdationDateTime = now;
                }

                await _db.SaveChangesAsync();
                _cache.Remove(ADMIN_CACHE_KEY);
                Logger.Info("UpdatePumpDetailsAsync: pumpId={0} updated via EF Core", req.PumpId);

                // Refresh today's daily summary in the background (fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var ist = TimeZoneInfo.FindSystemTimeZoneById(
                            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
                        var todayIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist).Date;
                        await _dailySummary.BuildSummaryForDateAsync(todayIst);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Background daily summary refresh failed for pumpId={0}", req.PumpId);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdatePumpDetailsAsync failed for pumpId={0}", req.PumpId);
                throw;
            }
        }

        // ── Get pump log history ──────────────────────────────────────────────
        public async Task<List<PumpLogDto>> GetPumpLogsAsync(int pumpId)
        {
            Logger.Debug("GetPumpLogsAsync: pumpId={0}", pumpId);
            try
            {
                // Fetch raw rows first — arithmetic on DateTime? can't be translated to MySQL
                var rows = await _db.PumpStatusLogs
                    .Where(l => l.PumpId == pumpId)
                    .OrderByDescending(l => l.RowInsertionDateTime)
                    .Take(50)
                    .AsNoTracking()
                    .ToListAsync();

                return rows.Select(l => new PumpLogDto
                {
                    LogId           = l.LogId,
                    OldStatus       = l.OldStatus.HasValue
                                        ? StatusLabel(l.OldStatus.Value) : "-",
                    NewStatus       = StatusLabel(l.NewStatus),
                    StartTime       = l.StartTime,
                    EndTime         = l.EndTime,
                    DurationMinutes = l.StartTime.HasValue && l.EndTime.HasValue
                                        ? (int)(l.EndTime.Value - l.StartTime.Value).TotalMinutes
                                        : null,
                    Remarks         = l.Remarks,
                    UpdatedBy       = l.UpdatedBy
                }).ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetPumpLogsAsync failed for pumpId={0}", pumpId);
                throw;
            }
        }

        // ── Assign operator to pump ───────────────────────────────────────────
        public async Task<bool> AssignOperatorAsync(AssignOperatorRequest req)
        {
            Logger.Info("AssignOperatorAsync: pumpId={0}, operator={1}", req.PumpId, req.OperatorMobile);
            try
            {
                var entry = await _db.PumpStatusEntries.FindAsync(req.PumpId);
                if (entry == null)
                {
                    Logger.Warn("AssignOperatorAsync: no status entry for pumpId={0}", req.PumpId);
                    return false;
                }

                entry.UpdatedBy           = req.UpdatedBy;
                entry.OperatorMobile      = req.OperatorMobile;
                entry.RowUpdationDateTime = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _cache.Remove(ADMIN_CACHE_KEY);
                Logger.Info("AssignOperatorAsync: pumpId={0} assigned to {1}", req.PumpId, req.OperatorMobile);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AssignOperatorAsync failed for pumpId={0}", req.PumpId);
                throw;
            }
        }

        // ── Get active operators and JEs for pump drawer dropdowns ────────────
        public async Task<List<UserSelectionDto>> GetActiveUsersForDrawerAsync()
        {
            try
            {
                return await _db.BdaUserMasters
                    .Where(u => u.IsActive && u.MobileNumber != null &&
                           (u.UserType == BdaUserType.OPERATOR || u.UserType == BdaUserType.JE))
                    .AsNoTracking()
                    .OrderBy(u => u.UserType).ThenBy(u => u.Name)
                    .Select(u => new UserSelectionDto
                    {
                        Mobile   = u.MobileNumber!,
                        Name     = u.Name ?? u.MobileNumber!,
                        UserType = u.UserType.HasValue ? u.UserType.Value.ToString() : ""
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetActiveUsersForDrawerAsync failed");
                return new List<UserSelectionDto>();
            }
        }

        // ── Pump Running Summary (today + this month run hours per pump) ──────
        public async Task<List<PumpRunningSummaryDto>> GetPumpRunningSummaryAsync(
            int? userId = null, string? userType = "ADMIN")
        {
            Logger.Debug("GetPumpRunningSummaryAsync: userId={0}, userType={1}", userId, userType);
            try
            {
                var nowUtc = DateTime.UtcNow;
                var ist = TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
                var nowIst    = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ist);
                var todayIst  = nowIst.Date;
                var monthStartIst = new DateTime(nowIst.Year, nowIst.Month, 1);

                // Mobile filter — OPERATOR filters by operator_mobile, JE filters by je_mobile
                string? operatorMobile = null;
                string? jeMobile = null;
                if (userType != "ADMIN" && userId.HasValue)
                {
                    var userMobile = await _db.BdaUserMasters
                        .Where(u => u.UserId == userId.Value && u.IsActive)
                        .Select(u => u.MobileNumber)
                        .FirstOrDefaultAsync();

                    if (userType == "JE")
                        jeMobile = userMobile;
                    else
                        operatorMobile = userMobile;
                }

                // Fetch active pumps with current status entry
                var pumpData = await (
                    from pump  in _db.BdaPumpMasters
                    where pump.IsActive
                    join loc   in _db.BdaPumpLocations  on pump.PumpId equals loc.PumpId   into lg
                    from loc   in lg.DefaultIfEmpty()
                    join entry in _db.PumpStatusEntries on pump.PumpId equals entry.PumpId into eg
                    from entry in eg.DefaultIfEmpty()
                    where (operatorMobile == null && jeMobile == null)
                       || (operatorMobile != null && entry != null && entry.OperatorMobile == operatorMobile)
                       || (jeMobile       != null && entry != null && entry.JeMobile       == jeMobile)
                    select new
                    {
                        pump.PumpId,
                        pump.VendorName,
                        LocationName     = loc   != null ? loc.LocationName          : null,
                        EntryStatus      = entry != null ? (PumpStatus?)entry.Status : null,
                        LastUpdated      = entry != null ? entry.RowUpdationDateTime : pump.RowUpdationDateTime,
                        OperatorMobile   = entry != null ? entry.OperatorMobile      : null,
                    }
                ).AsNoTracking().ToListAsync();

                var pumpIds = pumpData.Select(p => p.PumpId).ToList();

                // ── Pull pre-computed daily summaries for this month ─────────
                var dailySummaries = await _db.PumpDailySummaries
                    .Where(s => pumpIds.Contains(s.PumpId)
                             && s.SummaryDate >= monthStartIst
                             && s.SummaryDate <= todayIst)
                    .AsNoTracking()
                    .ToListAsync();

                // Build operator name lookup
                var mobileSet = pumpData
                    .Where(p => p.OperatorMobile != null)
                    .Select(p => p.OperatorMobile!)
                    .Distinct().ToList();
                var nameDict = (await _db.BdaUserMasters
                    .Where(u => mobileSet.Contains(u.MobileNumber!))
                    .AsNoTracking()
                    .Select(u => new { u.MobileNumber, u.Name })
                    .ToListAsync())
                    .ToDictionary(u => u.MobileNumber!, u => u.Name ?? "");

                var result = pumpData.Select(p =>
                {
                    var pumpSummaries = dailySummaries.Where(s => s.PumpId == p.PumpId).ToList();

                    // Today's breakdown (single row for today)
                    var todaySummary = pumpSummaries.FirstOrDefault(s => s.SummaryDate == todayIst);
                    int todayOn   = todaySummary?.OnMinutes          ?? 0;
                    int todayOff  = todaySummary?.OffMinutes         ?? 0;
                    int todayMnt  = todaySummary?.MaintenanceMinutes ?? 0;

                    // This month's breakdown (sum all days in month)
                    int monthOn   = pumpSummaries.Sum(s => s.OnMinutes);
                    int monthOff  = pumpSummaries.Sum(s => s.OffMinutes);
                    int monthMnt  = pumpSummaries.Sum(s => s.MaintenanceMinutes);

                    return new PumpRunningSummaryDto
                    {
                        PumpId                   = p.PumpId.ToString(),
                        VendorName               = p.VendorName,
                        Location                 = p.LocationName,
                        Status                   = p.EntryStatus switch
                        {
                            PumpStatus.On          => "ON",
                            PumpStatus.Maintenance => "MAINTENANCE",
                            _                      => "OFF"
                        },
                        TodayRunMinutes          = Math.Max(0, todayOn),
                        TodayOffMinutes          = Math.Max(0, todayOff),
                        TodayMaintenanceMinutes  = Math.Max(0, todayMnt),
                        MonthRunMinutes          = Math.Max(0, monthOn),
                        MonthOffMinutes          = Math.Max(0, monthOff),
                        MonthMaintenanceMinutes  = Math.Max(0, monthMnt),
                        LastUpdated              = p.LastUpdated,
                        OperatorName             = p.OperatorMobile != null && nameDict.TryGetValue(p.OperatorMobile, out var opN) ? opN : null,
                    };
                }).ToList();

                Logger.Debug("GetPumpRunningSummaryAsync: summary built for {0} pumps", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetPumpRunningSummaryAsync failed");
                throw;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private static string StatusLabel(PumpStatus s) => s switch
        {
            PumpStatus.On          => "ON",
            PumpStatus.Maintenance => "MAINTENANCE",
            _                      => "OFF"
        };

        // ── Add new pump (master + location rows) ─────────────────────────────
        public async Task<int> AddPumpAsync(AddPumpRequest req)
        {
            Logger.Info("AddPumpAsync: vendor={0}, location={1}", req.VendorName, req.LocationName);
            var now = DateTime.UtcNow;

            var pump = new BdaPumpMaster
            {
                VendorName           = req.VendorName,
                Category             = req.Category,
                IsActive             = true,
                UpdatedBy            = req.UpdatedBy,
                RowActionCount       = 1,
                RowInsertionDateTime = now,
                RowUpdationDateTime  = now
            };

            _db.BdaPumpMasters.Add(pump);
            await _db.SaveChangesAsync(); // generates PumpId via auto-increment

            _db.BdaPumpLocations.Add(new BdaPumpLocation
            {
                PumpId               = pump.PumpId,
                LocationName         = req.LocationName,
                Latitude             = req.Latitude,
                Longitude            = req.Longitude,
                RowActionCount       = 1,
                RowInsertionDateTime = now,
                RowUpdationDateTime  = now
            });

            // Seed an initial OFF status entry so the pump appears on the dashboard
            _db.PumpStatusEntries.Add(new PumpStatusEntry
            {
                PumpId               = pump.PumpId,
                Status               = PumpStatus.Off,
                UpdatedBy            = req.UpdatedBy,
                OperatorMobile       = string.IsNullOrEmpty(req.OperatorMobile) ? null : req.OperatorMobile,
                JeMobile             = string.IsNullOrEmpty(req.JeMobile) ? null : req.JeMobile,
                RowActionCount       = 1,
                RowInsertionDateTime = now,
                RowUpdationDateTime  = now
            });

            await _db.SaveChangesAsync();

            _cache.Remove(ADMIN_CACHE_KEY);
            Logger.Info("AddPumpAsync: created pumpId={0}, cache cleared", pump.PumpId);
            return pump.PumpId;
        }
    }

    public class DashboardPumpDto
    {
        public string PumpId { get; set; } = "";
        public string? VendorName { get; set; }
        public string? Location { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Status { get; set; }
        public int RunningMinutes { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? OperatorName { get; set; }
        public string? OperatorMobile { get; set; }
        public string? JeName { get; set; }
        public string? JeMobile { get; set; }
    }

    public class UserSelectionDto
    {
        public string Mobile   { get; set; } = "";
        public string Name     { get; set; } = "";
        public string UserType { get; set; } = "";
    }

    public class PumpRunningSummaryDto
    {
        public string PumpId { get; set; } = "";
        public string? VendorName { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }

        // Today breakdown
        public int TodayRunMinutes { get; set; }
        public int TodayOffMinutes { get; set; }
        public int TodayMaintenanceMinutes { get; set; }

        // This month breakdown
        public int MonthRunMinutes { get; set; }
        public int MonthOffMinutes { get; set; }
        public int MonthMaintenanceMinutes { get; set; }

        public DateTime LastUpdated { get; set; }
        public string? OperatorName { get; set; }
    }
}
