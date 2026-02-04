using asset_monitoring.Data;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace asset_monitoring.Services
{
    public class PumpDashboardService
    {
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
                    return cachedPumps;
                }

                var pumps = await FetchFromDatabaseAsync(null, "ADMIN");

                _cache.Set(
                    ADMIN_CACHE_KEY,
                    pumps,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2)
                    });

                return pumps;
            }

            // 🔹 NON-ADMIN: always fetch fresh (user-specific)
            return await FetchFromDatabaseAsync(userId, user_type);
        }

        private async Task<List<DashboardPumpDto>> FetchFromDatabaseAsync(
            int? userId,
            string? user_type)
        {
            var pumps = new List<DashboardPumpDto>();

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

            return pumps;
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

            await cmd.ExecuteNonQueryAsync();

            // ❗ Clear cache after update
            _cache.Remove(ADMIN_CACHE_KEY);
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
