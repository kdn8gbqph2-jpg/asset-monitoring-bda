using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class BdaPumpLocationConfig : IEntityTypeConfiguration<BdaPumpLocation>
    {
        public void Configure(EntityTypeBuilder<BdaPumpLocation> entity)
        {
            entity.ToTable("bda_pump_location_tbl");

            entity.HasKey(e => e.PumpId);

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id");

            entity.Property(e => e.LocationName)
                  .HasColumnName("location_name")
                  .HasMaxLength(150);

            entity.Property(e => e.Latitude)
                  .HasColumnName("latitude")
                  .HasColumnType("decimal(10,8)");

            entity.Property(e => e.Longitude)
                  .HasColumnName("longitude")
                  .HasColumnType("decimal(11,8)");

            entity.Property(e => e.RowActionCount)
                  .HasColumnName("row_action_count")
                  .HasDefaultValue(1);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            // Foreign key to bda_pump_master_tbl (pump_id) — keep delete behavior restrictive to match typical FK without ON DELETE
            entity.HasOne(e => e.PumpMaster)
                  .WithOne()
                  .HasForeignKey<BdaPumpLocation>(e => e.PumpId)
                  .HasConstraintName("fk_pump_location")
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}