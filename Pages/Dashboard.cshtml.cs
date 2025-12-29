using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using asset_monitoring.Data;
using asset_monitoring.Models;
using NLog; // Add this
using Microsoft.Extensions.Configuration; // Add this

namespace asset_monitoring.Pages
{
    public class DashboardModel : PageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // Add this

        private readonly ApplicationDbContext _db;

        public DashboardModel(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            MapCenterLatitude = config.GetValue<decimal>("MapSettings:CenterLatitude");
            MapCenterLongitude = config.GetValue<decimal>("MapSettings:CenterLongitude");
            MapZoom = config.GetValue<int>("MapSettings:Zoom");
        }

        public int TotalPumps { get; private set; }
        public int RunningPumpCount { get; private set; }
        public string AvgRuntime { get; private set; } = "0 hrs";

        public List<PumpRow> Pumps { get; private set; } = new();

        [BindProperty]
        public LoginInputModel LoginModel { get; set; } = new();

        public string? LoginMessage { get; private set; }

        public decimal MapCenterLatitude { get; private set; }
        public decimal MapCenterLongitude { get; private set; }
        public int MapZoom { get; private set; }

        public async Task OnGetAsync()
        {
            Logger.Info("Dashboard OnGetAsync started at {0}", DateTime.UtcNow);

            Pumps = new List<PumpRow>();
            this.RunningPumpCount = 0;
            this.TotalPumps = 0;

            try
            {
                await using var conn = (MySqlConnection)_db.Database.GetDbConnection();
                await conn.OpenAsync();

                await using var cmd = new MySqlCommand("sp_get_pump_dashboard_data", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await using var reader = await cmd.ExecuteReaderAsync();

                double totalRunningMinutes = 0;

                while (await reader.ReadAsync())
                {
                    var status = reader["pump_status"].ToString();

                    int runningMinutes = Convert.ToInt32(reader["running_time_minutes"]);
                    totalRunningMinutes += runningMinutes;

                    Pumps.Add(new PumpRow
                    {
                        PumpId = reader["pump_id"].ToString()!,
                        VendorName = reader["vendor_name"].ToString(),
                        Location = reader["location_name"].ToString(),
                        Latitude = reader["latitude"] == DBNull.Value ? null : Convert.ToDecimal(reader["latitude"]),
                        Longitude = reader["longitude"] == DBNull.Value ? null : Convert.ToDecimal(reader["longitude"]),
                        Status = status,
                        LastUpdated = Convert.ToDateTime(reader["last_updated_time"]),
                        LastRun = null
                    });

                    if (status == "ON")
                        this.RunningPumpCount++;
                }

                this.TotalPumps = Pumps.Count;

                var avgHours = this.TotalPumps > 0
                    ? (totalRunningMinutes / 60.0) / this.TotalPumps
                    : 0;

                this.AvgRuntime = $"{avgHours:0.#} hrs";

                Logger.Info("Dashboard data loaded: TotalPumps={0}, RunningPumpCount={1}, AvgRuntime={2}",
                    this.TotalPumps, this.RunningPumpCount, this.AvgRuntime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading dashboard data");
                throw;
            }
        }

        public async Task<IActionResult> OnPostLoginAsync()
        {
            Logger.Info("Login attempt for user: {0}", LoginModel.Username);

            if (LoginModel.Username == "admin" && LoginModel.Password == "password")
            {
                LoginMessage = "Login successful";
                Logger.Info("Login successful for user: {0}", LoginModel.Username);
            }
            else
            {
                LoginMessage = "Invalid username or password";
                Logger.Warn("Login failed for user: {0}", LoginModel.Username);
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<JsonResult> OnGetRefreshAsync()
        {
            Logger.Info("Dashboard refresh requested at {0}", DateTime.UtcNow);
            await OnGetAsync();
            return new JsonResult(new
            {
                totalPumps = TotalPumps,
                runningNow = RunningPumpCount,
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
            public DateTime LastUpdated { get; init; }
            public DateTime? LastRun { get; init; }
        }

        public class LoginInputModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
