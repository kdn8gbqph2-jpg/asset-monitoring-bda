
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class PumpStatusLogConfig : IEntityTypeConfiguration<PumpStatusLog>
    {
        public void Configure(EntityTypeBuilder<PumpStatusLog> entity)
        {
            entity.ToTable("pump_status_log_tbl");

            entity.HasKey(e => e.LogId);

            entity.Property(e => e.LogId)
                  .HasColumnName("log_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id");

            entity.HasIndex(e => e.PumpId)
                  .HasDatabaseName("fk_pump_status_log_pump");

            entity.Property(e => e.Location)
                  .HasColumnName("location")
                  .HasMaxLength(150);

            entity.Property(e => e.OldStatus)
                  .HasColumnName("old_status")
                  .HasConversion(PumpStatusConverter.NullableInstance)
                  .HasColumnType("enum('ON','OFF','MAINTENANCE')");

            entity.Property(e => e.NewStatus)
                  .HasColumnName("new_status")
                  .HasConversion(PumpStatusConverter.Instance)
                  .HasColumnType("enum('ON','OFF','MAINTENANCE')")
                  .IsRequired();

            entity.Property(e => e.StartTime)
                  .HasColumnName("start_time");

            entity.Property(e => e.EndTime)
                  .HasColumnName("end_time");

            entity.Property(e => e.Remarks)
                  .HasColumnName("remarks")
                  .HasMaxLength(255);

            entity.Property(e => e.UpdatedBy)
                  .HasColumnName("updated_by")
                  .HasMaxLength(100);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            entity.HasOne(e => e.PumpMaster)
                  .WithMany()
                  .HasForeignKey(e => e.PumpId)
                  .HasConstraintName("fk_pump_status_log_pump")
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}