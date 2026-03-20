using asset_monitoring.Data;
using asset_monitoring.Services;
using Microsoft.AspNetCore.HttpOverrides;
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
    builder.Services.AddSingleton<DailySummaryService>();
    builder.Services.AddHostedService<DailySummaryBackgroundService>();

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout      = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly  = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite    = SameSiteMode.Strict;
        // Always require Secure cookie in production; allow HTTP in development
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

    // Trust the X-Forwarded-* headers sent by Nginx
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Only trust localhost (Nginx runs on same machine)
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    // Must be first — reads X-Forwarded-Proto so HTTPS detection works correctly
    app.UseForwardedHeaders();

    // ── Security headers ──────────────────────────────────────────────────
    app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"]  = "nosniff";
        h["X-Frame-Options"]         = "SAMEORIGIN";
        h["X-XSS-Protection"]        = "1; mode=block";
        h["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        h["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=()";
        if (!ctx.Request.IsHttps) { /* HSTS only over HTTPS */ }
        else h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        await next();
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // HSTS handled by Nginx in production; skip in non-dev to avoid double headers
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();
    app.UseAuthorization();
    app.MapRazorPages();

    logger.Info("Application starting — Environment={0}", app.Environment.EnvironmentName);
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
