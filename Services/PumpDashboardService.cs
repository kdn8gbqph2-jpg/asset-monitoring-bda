using asset_monitoring.Data;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Data;
using NLog;

namespace asset_monitoring.Services
{
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

        // 🔹 Update pump + clear cache
        public async Task UpdatePumpDetailsAsync(
            int pumpId,
            string vendorName,
            string? category,
            string locationName,
            string status,
            string latitude,
            string longitude,
            bool isActive)
        {
            Logger.Info("UpdatePumpDetailsAsync: updating pumpId={0}, status={1}, isActive={2}", pumpId, status, isActive);

            try
            {
                await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
                await conn.OpenAsync();

                await using var cmd = new MySqlCommand("sp_update_pump_details", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("p_pump_id", pumpId);
                cmd.Parameters.AddWithValue("p_vendor_name", vendorName);
                cmd.Parameters.AddWithValue("p_category", category ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("p_location_name", locationName);
                cmd.Parameters.AddWithValue("p_status", status);
                cmd.Parameters.AddWithValue("p_latitude", latitude);
                cmd.Parameters.AddWithValue("p_longitude", longitude);
                cmd.Parameters.AddWithValue("p_is_active", isActive ? 1 : 1);

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows <= 0)
                    Logger.Warn("UpdatePumpDetailsAsync: sp_update_pump_details affected 0 rows for pumpId={0}", pumpId);
                else
                    Logger.Info("UpdatePumpDetailsAsync: pumpId={0} updated successfully", pumpId);

                // ❗ Clear cache after update
                _cache.Remove(ADMIN_CACHE_KEY);
                Logger.Debug("UpdatePumpDetailsAsync: admin cache cleared after update of pumpId={0}", pumpId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UpdatePumpDetailsAsync failed for pumpId={0}", pumpId);
                throw;
            }
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
