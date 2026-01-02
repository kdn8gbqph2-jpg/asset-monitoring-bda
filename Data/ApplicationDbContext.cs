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

        public DbSet<ActiveUsers> ActiveUsers => Set<ActiveUsers>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new BdaPumpMasterConfig());
            modelBuilder.ApplyConfiguration(new BdaPumpLocationConfig());
            modelBuilder.ApplyConfiguration(new BdaUserMasterConfig());
            modelBuilder.ApplyConfiguration(new PumpStatusEntryConfig());
            modelBuilder.ApplyConfiguration(new PumpStatusLogConfig());

            modelBuilder.Entity<ActiveUsers>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null); // IMPORTANT for stored procedure

                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.UserType).HasColumnName("user_type");
                entity.Property(e => e.MobileNumber).HasColumnName("mobile_number");
                entity.Property(e => e.Password).HasColumnName("password");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}