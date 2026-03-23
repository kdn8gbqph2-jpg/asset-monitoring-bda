using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class AppConfigConfig : IEntityTypeConfiguration<AppConfig>
    {
        public void Configure(EntityTypeBuilder<AppConfig> entity)
        {
            entity.ToTable("app_config_tbl");

            entity.HasKey(e => e.ConfigId);

            entity.Property(e => e.ConfigId)
                  .HasColumnName("config_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.ConfigKey)
                  .HasColumnName("config_key")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.HasIndex(e => e.ConfigKey)
                  .IsUnique()
                  .HasDatabaseName("uq_app_config_key");

            entity.Property(e => e.ConfigValue)
                  .HasColumnName("config_value")
                  .HasMaxLength(2000);

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        }
    }
}
