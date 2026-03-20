using asset_monitoring.Data;
using asset_monitoring.Services;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using QuestPDF.Infrastructure;
using System.IO;

LogManager.Setup().LoadConfigurationFromAppSettings();
var logger = LogManager.GetCurrentClassLogger();

QuestPDF.Settings.License = LicenseType.Community;

try
{
    // Ensure logs directory exists (NLog creates files, but not the directory)
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));

    var builder = WebApplication.CreateBuilder(args);

    // Use NLog as the logging provider
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    builder.Services.AddRazorPages();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<UserCacheService>();
    builder.Services.AddScoped<PumpDashboardService>();
    builder.Services.AddScoped<ReportExportService>();

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();
    app.UseAuthorization();
    app.MapRazorPages();

    logger.Info("Application starting up");
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}
