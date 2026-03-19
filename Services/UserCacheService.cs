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

        // Replaces sp_get_active_users() with a direct EF Core query.
        // UserName is set to MobileNumber because login uses mobile as the key.
        private void Load()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var users = db.BdaUserMasters
                    .Where(u => u.IsActive)
                    .AsNoTracking()
                    .Select(u => new ActiveUsers
                    {
                        UserId       = u.UserId,
                        Name         = u.Name         ?? "",
                        UserName     = u.MobileNumber ?? "",   // mobile is the login key
                        UserType     = u.UserType.HasValue
                                           ? u.UserType.Value.ToString()
                                           : "",
                        Password     = u.Password     ?? "",
                        MobileNumber = u.MobileNumber ?? ""
                    })
                    .ToList();

                Users = users
                    .Where(u => !string.IsNullOrWhiteSpace(u.UserName))
                    .ToDictionary(u => u.UserName, u => u);

                Logger.Info("UserCacheService.Load: {0} active users loaded into cache", Users.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "UserCacheService.Load: failed to load user cache");
                throw;
            }
        }

        public void Reload()
        {
            Logger.Info("UserCacheService.Reload: reloading user cache");
            Load();
        }
    }
}
