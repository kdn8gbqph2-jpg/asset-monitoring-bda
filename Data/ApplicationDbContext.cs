using asset_monitoring.Data.Configurations;
using asset_monitoring.Models;
using Microsoft.EntityFrameworkCore;

namespace asset_monitoring.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BdaPumpMaster> BdaPumpMasters { get; set; } = null!;
        public DbSet<BdaPumpLocation> BdaPumpLocations { get; set; } = null!;
        public DbSet<BdaUserMaster> BdaUserMasters { get; set; } = null!;
        public DbSet<PumpStatusLog> PumpStatusLogs { get; set; } = null!;
        public DbSet<PumpStatusEntry> PumpStatusEntries { get; set; } = null!;
        public DbSet<PumpDailySummary> PumpDailySummaries { get; set; } = null!;
        public DbSet<ComplaintLog> ComplaintLogs { get; set; } = null!;
        public DbSet<AppConfig> AppConfigs { get; set; } = null!;
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new BdaPumpMasterConfig());
            modelBuilder.ApplyConfiguration(new BdaPumpLocationConfig());
            modelBuilder.ApplyConfiguration(new BdaUserMasterConfig());
            modelBuilder.ApplyConfiguration(new PumpStatusEntryConfig());
            modelBuilder.ApplyConfiguration(new PumpStatusLogConfig());
            modelBuilder.ApplyConfiguration(new PumpDailySummaryConfig());
            modelBuilder.ApplyConfiguration(new ComplaintLogConfig());
            modelBuilder.ApplyConfiguration(new AppConfigConfig());
            modelBuilder.ApplyConfiguration(new PushSubscriptionConfig());

            base.OnModelCreating(modelBuilder);
        }
    }
}