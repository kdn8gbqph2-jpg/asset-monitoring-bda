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

        public async Task<List<DashboardPumpDto>> GetPumpsAsync()
        {
            var pumps = new List<DashboardPumpDto>();

            await using var conn = (MySqlConnection)_db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand("sp_get_pump_dashboard_data", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

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
                    LastUpdated = Convert.ToDateTime(reader["last_updated_time"])
                });
            }

            return pumps;
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
    }
}
