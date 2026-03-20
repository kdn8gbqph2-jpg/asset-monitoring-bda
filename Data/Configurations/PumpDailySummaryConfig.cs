using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class PumpDailySummaryConfig : IEntityTypeConfiguration<PumpDailySummary>
    {
        public void Configure(EntityTypeBuilder<PumpDailySummary> entity)
        {
            entity.ToTable("pump_daily_summary_tbl");

            entity.HasKey(e => e.SummaryId);

            entity.Property(e => e.SummaryId)
                  .HasColumnName("summary_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id");

            entity.Property(e => e.SummaryDate)
                  .HasColumnName("summary_date")
                  .HasColumnType("date");

            // Unique constraint: one row per pump per day
            entity.HasIndex(e => new { e.PumpId, e.SummaryDate })
                  .IsUnique()
                  .HasDatabaseName("uq_pump_daily_summary");

            entity.Property(e => e.OnMinutes)
                  .HasColumnName("on_minutes")
                  .HasDefaultValue(0);

            entity.Property(e => e.OffMinutes)
                  .HasColumnName("off_minutes")
                  .HasDefaultValue(0);

            entity.Property(e => e.MaintenanceMinutes)
                  .HasColumnName("maintenance_minutes")
                  .HasDefaultValue(0);

            entity.Property(e => e.StatusChangeCount)
                  .HasColumnName("status_change_count")
                  .HasDefaultValue(0);

            entity.Property(e => e.FirstStatus)
                  .HasColumnName("first_status")
                  .HasConversion(PumpStatusConverter.NullableInstance)
                  .HasColumnType("enum('ON','OFF','MAINTENANCE')");

            entity.Property(e => e.LastStatus)
                  .HasColumnName("last_status")
                  .HasConversion(PumpStatusConverter.NullableInstance)
                  .HasColumnType("enum('ON','OFF','MAINTENANCE')");

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            entity.HasOne(e => e.PumpMaster)
                  .WithMany()
                  .HasForeignKey(e => e.PumpId)
                  .HasConstraintName("fk_daily_summary_pump")
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
