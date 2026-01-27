using asset_monitoring.Data;
using asset_monitoring.Models;
using asset_monitoring.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; 
using MySqlConnector;
using NLog; 
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace asset_monitoring.Pages
{
    public class DashboardModel : PageModel
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); 

        private readonly ApplicationDbContext _db;

        private readonly UserCacheService _userCache;
        private readonly PumpDashboardService _PumpdashboardService;


        public DashboardModel(ApplicationDbContext db, 
            IConfiguration config, UserCacheService userCache, PumpDashboardService pumpdashboardService)
        {
            _db = db;
            _userCache = userCache;
            _PumpdashboardService = pumpdashboardService;
            MapCenterLatitude = config.GetValue<decimal>("MapSettings:CenterLatitude");
            MapCenterLongitude = config.GetValue<decimal>("MapSettings:CenterLongitude");
            MapZoom = config.GetValue<int>("MapSettings:Zoom");
            _PumpdashboardService = pumpdashboardService;
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

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalPages { get; set; }
        public List<PumpRow> PagedPumps { get; private set; } = new();

        public async Task OnGetAsync(int? pageNumber = 1)
        {
            Logger.Info("Dashboard OnGetAsync started");

            var pumpData = await _PumpdashboardService.GetPumpsAsync(LoginModel.Username);

            Pumps = pumpData.Select(p => new PumpRow
            {
                PumpId = p.PumpId,
                VendorName = p.VendorName,
                Location = p.Location,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Status = p.Status,
                LastUpdated = p.LastUpdated,
                LastRun = null,
                MobileNumber = p.MobileNumber // <-- Add this line
            }).ToList();

            TotalPumps = Pumps.Count;
            RunningPumpCount = pumpData.Count(p => p.Status == "ON");

            double totalRunningMinutes = pumpData.Sum(p => p.RunningMinutes);
            AvgRuntime = TotalPumps > 0
                ? $"{(totalRunningMinutes / 60.0 / TotalPumps):0.#} hrs"
                : "0 hrs";

            PageNumber = pageNumber ?? 1;
            TotalPages = (int)Math.Ceiling(Pumps.Count / (double)PageSize);
            PagedPumps = Pumps.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
        }


        public async Task<IActionResult> OnPostLoginAsync()
        {
            Logger.Info("Login attempt for user: {0}", LoginModel.Username);

            if (string.IsNullOrWhiteSpace(LoginModel.Username) ||
                string.IsNullOrWhiteSpace(LoginModel.Password))
            {
                LoginMessage = "Username and password are required.";
                await OnGetAsync();
                return Page();
            }

            // Lookup user from cache (key = mobile number)
            if (!_userCache.Users.TryGetValue(LoginModel.Username, out var user))
            {
                Logger.Warn("Login failed (user not found): {0}", LoginModel.Username);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            // PASSWORD CHECK (plain text version — replace with hash later)
            if (user.Password != LoginModel.Password)
            {
                Logger.Warn("Login failed (wrong password) for user: {0}", LoginModel.Username);
                LoginMessage = "Invalid username or password";
                await OnGetAsync();
                return Page();
            }

            // Login success
            Logger.Info("Login successful for user: {0}, role={1}", user.Name, user.UserType);

            LoginMessage = $"Welcome {user.Name} ({user.UserType})";

            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserType", user.UserType);
            HttpContext.Session.SetString("Mobile", user.MobileNumber);

            if (user.UserType == "ADMIN")
            {
                return RedirectToPage("/Admin");
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
            public string? MobileNumber { get; init; } // <-- Add this line
        }

        public class LoginInputModel
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
