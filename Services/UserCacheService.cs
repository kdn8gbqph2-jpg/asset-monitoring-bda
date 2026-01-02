namespace asset_monitoring.Services
{
    using asset_monitoring.Data;
    using asset_monitoring.Models;
    using Microsoft.EntityFrameworkCore;

    public class UserCacheService
    {
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var users = db.ActiveUsers
                .FromSqlRaw("CALL sp_get_active_users()")
                .AsNoTracking()
                .ToList();

            Users = users
                .Where(u => !string.IsNullOrWhiteSpace(u.MobileNumber))
                .ToDictionary(u => u.MobileNumber, u => u);
        }

        // Optional: manual reload
        public void Reload()
        {
            Load();
        }
    }

}
