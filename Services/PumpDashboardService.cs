using asset_monitoring.Data;
using asset_monitoring.Models;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Data;
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
    }

    public class AddPumpRequest
    {
        public string VendorName { get; set; } = "";
        public string? Category { get; set; }
        public string LocationName { get; set; } = "";
        public string Status { get; set; } = "OFF";
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class PumpDashboardService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        private const string ADMIN_CACHE_KEY = "PUMP_DASHBOARD_ADMIN";

        public PumpDashboardService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<DashboardPumpDto>> GetPumpsAsync(
            int? userId = null,
            string? user_type = "ADMIN")
        {
            // 🔹 ADMIN: cached data
            if (user_type == "ADMIN")
            {
                if (_cache.TryGetValue(ADMIN_CACHE_KEY, out List<DashboardPumpDto> cachedPumps))
                {
                    Logger.Debug("GetPumpsAsync: returning {0} pumps from cache for ADMIN", cachedPumps.Count);
                    return cachedPumps;
                }

                Logger.Debug("GetPumpsAsync: cache miss for ADMIN, fetching from DB");
                var pumps = await FetchFromDatabaseAsync(null, "ADMIN");

                _cache.Set(
                    ADMIN_CACHE_KEY,
                    pumps,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });

                Logger.Info("GetPumpsAsync: loaded {0} pumps from DB and cached for ADMIN", pumps.Count);
                return pumps;
            }

            // 🔹 NON-ADMIN: always fetch fresh (user-specific)
            Logger.Debug("GetPumpsAsync: fetching fresh data for userId={0}, userType={1}", userId, user_type);
            return await FetchFromDatabaseAsync(userId, user_type);
        }

        private async Task<List<DashboardPumpDto>> FetchFromDatabaseAsync(
            int? userId,
            string? user_type)
        {
            var pumps = new List<DashboardPumpDto>();

            try
            {
                await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
                await conn.OpenAsync();

                await using var cmd = new MySqlCommand("sp_get_pump_dashboard_data", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("p_userid", userId);
                cmd.Parameters.AddWithValue("p_user_type", user_type);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    pumps.Add(new DashboardPumpDto
                    {
                        PumpId = reader["pump_id"].ToString()!,
                        VendorName = reader["vendor_name"]?.ToString(),
                        Location = reader["location_name"]?.ToString(),
                        Latitude = reader["latitude"] == DBNull.Value ? null : Convert.ToDecimal(reader["latitude"]),
                        Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDecimal(reader["longitude"]),
                        Status = reader["pump_status"]?.ToString(),
                        RunningMinutes = Convert.ToInt32(reader["running_time_minutes"]),
                        LastUpdated = Convert.ToDateTime(reader["last_updated_time"]),
                        MobileNumber = reader["operator_mobile"]?.ToString()
                    });
                }

                Logger.Debug("FetchFromDatabaseAsync: retrieved {0} pumps for userId={1}, userType={2}", pumps.Count, userId, user_type);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "FetchFromDatabaseAsync failed for userId={0}, userType={1}", userId, user_type);
                throw;
            }

            return pumps;
        }

        // 🔹 Soft-delete pump + clear cache
        public async Task<bool> DeletePumpAsync(int pumpId)
        {
            Logger.Info("DeletePumpAsync: soft-deleting pumpId={0}", pumpId);

            var pump = await _db.BdaPumpMasters.FindAsync(pumpId);
            if (pump == null)
            {
                Logger.Warn("DeletePumpAsync: pumpId={0} not found", pumpId);
                return false;
            }

            pump.IsActive = false;
            _db.BdaPumpMasters.Update(pump);
            await _db.SaveChangesAsync();

            _cache.Remove(ADMIN_CACHE_KEY);
            Logger.Info("DeletePumpAsync: pumpId={0} marked inactive, cache cleared", pumpId);
            return true;
        }

        // 🔹 Update pump details + clear cache
        public async Task UpdatePumpDetailsAsync(UpdatePumpRequest req)
        {
            Logger.Info("UpdatePumpDetailsAsync: pumpId={0}, status={1}, isActive={2}", req.PumpId, req.Status, req.IsActive);

            try
            {
                await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
                await conn.OpenAsync();

                await using var cmd = new MySqlCommand("sp_update_pump_details", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("p_pump_id", req.PumpId);
                cmd.Parameters.AddWithValue("p_vendor_name", req.VendorName);
                cmd.Parameters.AddWithValue("p_category", req.Category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("p_location_name", req.LocationName);
                cmd.Parameters.AddWithValue("p_status", req.Status);
                cmd.Parameters.AddWithValue("p_latitude", req.Latitude?.ToString() ?? "");
                cmd.Parameters.AddWithValue("p_longitude", req.Longitude?.ToString() ?? "");
                cmd.Parameters.AddWithValue("p_is_active", req.IsActive ? 1 : 0); // ✅ fixed: was always 1

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows <= 0)
                    Logger.Warn("UpdatePumpDetailsAsync: SP affected 0 rows for pumpId={0}", req.PumpId);
                else
                    Logger.Info("UpdatePumpDetailsAsync: pumpId={0} updated successfully", req.PumpId);

                _cache.Remove(ADMIN_CACHE_KEY);
                Logger.Debug("UpdatePumpDetailsAsync: admin cache cleared for pumpId={0}", req.PumpId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdatePumpDetailsAsync failed for pumpId={0}", req.PumpId);
                throw;
            }
        }

        // 🔹 Add new pump (master + location rows) + clear cache
        public async Task<int> AddPumpAsync(AddPumpRequest req)
        {
            Logger.Info("AddPumpAsync: vendor={0}, location={1}", req.VendorName, req.LocationName);

            var now = DateTime.UtcNow;

            var pump = new BdaPumpMaster
            {
                VendorName = req.VendorName,
                Category = req.Category,
                IsActive = true,
                RowActionCount = 1,
                RowInsertionDateTime = now,
                RowUpdationDateTime = now
            };

            _db.BdaPumpMasters.Add(pump);
            await _db.SaveChangesAsync(); // generates PumpId

            var location = new BdaPumpLocation
            {
                PumpId = pump.PumpId,
                LocationName = req.LocationName,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                RowActionCount = 1,
                RowInsertionDateTime = now,
                RowUpdationDateTime = now
            };

            _db.BdaPumpLocations.Add(location);
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
        public string? MobileNumber { get; set; }
    }
}
