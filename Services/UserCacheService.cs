namespace asset_monitoring.Services
{
    using asset_monitoring.Data;
    using asset_monitoring.Models;
    using Microsoft.EntityFrameworkCore;
    using NLog;

    public class UserCacheService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IServiceScopeFactory _scopeFactory;

        public Dictionary<string, ActiveUsers> Users { get; private set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public UserCacheService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            Load();
        }

        private void Load()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var users = db.ActiveUsers
                    .FromSqlRaw("CALL sp_get_active_users()")
                    .AsNoTracking()
                    .ToList();

                Users = users
                    .Where(u => !string.IsNullOrWhiteSpace(u.UserName))
                    .ToDictionary(u => u.UserName, u => u);

                Logger.Info("UserCacheService.Load: loaded {0} active users into cache", Users.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UserCacheService.Load: failed to load user cache");
                throw;
            }
        }

        // Optional: manual reload
        public void Reload()
        {
            Logger.Info("UserCacheService.Reload: reloading user cache");
            Load();
        }
    }

}
