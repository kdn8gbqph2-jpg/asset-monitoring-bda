using asset_monitoring.Data;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace asset_monitoring.Services
{
    public class PumpDashboardService
    {
        private readonly ApplicationDbContext _db;

        public PumpDashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<DashboardPumpDto>> GetPumpsAsync(string? username = null, string? user_type = "ADMIN")
        {
            var pumps = new List<DashboardPumpDto>();

            await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand("sp_get_pump_dashboard_data", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add(new MySqlParameter("@p_username",
                string.IsNullOrWhiteSpace(username) ? DBNull.Value : username));
            cmd.Parameters.Add(new MySqlParameter("@p_user_type",
                string.IsNullOrWhiteSpace(user_type) ? DBNull.Value : user_type));

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
            cmd.Parameters.AddWithValue("p_is_active", isActive ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();
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
