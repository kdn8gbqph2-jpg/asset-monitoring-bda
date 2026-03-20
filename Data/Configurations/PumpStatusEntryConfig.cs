using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class PumpStatusEntryConfig : IEntityTypeConfiguration<PumpStatusEntry>
    {
        public void Configure(EntityTypeBuilder<PumpStatusEntry> entity)
        {
            entity.ToTable("pump_status_tbl");

            entity.HasKey(e => e.PumpId);

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id")
                  .ValueGeneratedNever();

            entity.Property(e => e.Status)
                  .HasColumnName("status")
                  .HasConversion(PumpStatusConverter.Instance)
                  .HasColumnType("enum('ON','OFF','MAINTENANCE')")
                  .IsRequired();

            entity.Property(e => e.Remarks)
                  .HasColumnName("remarks")
                  .HasMaxLength(255);

            entity.Property(e => e.CurrentStartTime)
                  .HasColumnName("current_start_time");

            entity.Property(e => e.CurrentEndTime)
                  .HasColumnName("current_end_time");

            entity.Property(e => e.LastRunTime)
                  .HasColumnName("last_run_time");

            entity.Property(e => e.UpdatedBy)
                  .HasColumnName("updated_by")
                  .HasMaxLength(100);

            entity.Property(e => e.OperatorMobile)
                  .HasColumnName("operator_mobile")
                  .HasMaxLength(15);

            entity.Property(e => e.JeMobile)
                  .HasColumnName("je_mobile")
                  .HasMaxLength(15);

            entity.Property(e => e.RowActionCount)
                  .HasColumnName("row_action_count")
                  .HasDefaultValue(1);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            // Primary key is also the foreign key to bda_pump_master_tbl (one-to-one)
            entity.HasOne(e => e.PumpMaster)
                  .WithOne()
                  .HasForeignKey<PumpStatusEntry>(e => e.PumpId)
                  .HasConstraintName("fk_pump_status_pump")
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}