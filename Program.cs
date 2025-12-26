using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using asset_monitoring.Data;
using NLog;
using NLog.Web;

// Use the new recommended NLog setup API
LogManager.Setup().LoadConfigurationFromAppSettings();
var logger = LogManager.GetCurrentClassLogger();

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
    logger.Info("Current connection string :", connectionString);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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
