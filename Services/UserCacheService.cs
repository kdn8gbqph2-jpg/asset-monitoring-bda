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
        // Cache is keyed by BOTH username and mobile number so either can be used to login.
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
                        UserName     = u.Username     ?? u.MobileNumber ?? "",
                        UserType     = u.UserType.HasValue
                                           ? u.UserType.Value.ToString()
                                           : "",
                        Password     = u.Password     ?? "",
                        MobileNumber = u.MobileNumber ?? ""
                    })
                    .ToList();

                // Index by username AND mobile number — whichever the user types at login works
                var dict = new Dictionary<string, ActiveUsers>(StringComparer.OrdinalIgnoreCase);
                foreach (var u in users)
                {
                    if (!string.IsNullOrWhiteSpace(u.UserName))
                        dict[u.UserName] = u;
                    if (!string.IsNullOrWhiteSpace(u.MobileNumber) && !dict.ContainsKey(u.MobileNumber))
                        dict[u.MobileNumber] = u;
                }
                Users = dict;

                Logger.Info("UserCacheService.Load: {0} active users loaded into cache ({1} keys)", users.Count, Users.Count);
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
