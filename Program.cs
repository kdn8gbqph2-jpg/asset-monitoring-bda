using asset_monitoring.Data;
using asset_monitoring.Services;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using QuestPDF.Infrastructure;
using System;
using System.IO;

// Use the new recommended NLog setup API
LogManager.Setup().LoadConfigurationFromAppSettings();
var logger = LogManager.GetCurrentClassLogger();

QuestPDF.Settings.License = LicenseType.Community;

try
{
    // create logs directory (NLog will create files, but ensure directory exists)
    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logsDir);

    var builder = WebApplication.CreateBuilder(args);

    // Override variable in nlog.config so file name uses application start timestamp (ms precision)
    LogManager.Configuration.Variables["starttime"] = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");

    LogManager.ReconfigExistingLoggers();

    // Use NLog as logging provider
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add services to the container.
    builder.Services.AddRazorPages();

    // Read connection string and register DbContext for MySQL (Pomelo)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    logger.Info("Current connection string :" + connectionString);

    //Services
    builder.Services.AddSingleton<UserCacheService>();
    builder.Services.AddScoped<PumpDashboardService>();
    builder.Services.AddScoped<ReportExportService>();
    builder.Services.AddScoped<UserService>();

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });
    var app = builder.Build();

    // Configure the HTTP request pipeline.
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

    logger.Info("Starting web host, logs directory: {0}", logsDir);
    app.Run();
}
catch (Exception ex)
{
    // Ensure exceptions during startup are logged
    logger.Error(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    LogManager.Shutdown();
}
