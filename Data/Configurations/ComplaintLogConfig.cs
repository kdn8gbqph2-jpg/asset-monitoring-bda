using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class ComplaintLogConfig : IEntityTypeConfiguration<ComplaintLog>
    {
        public void Configure(EntityTypeBuilder<ComplaintLog> entity)
        {
            entity.ToTable("complaint_log_tbl");

            entity.HasKey(e => e.ComplaintId);

            entity.Property(e => e.ComplaintId)
                  .HasColumnName("complaint_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id");

            entity.HasIndex(e => e.PumpId)
                  .HasDatabaseName("idx_complaint_log_pump");

            entity.Property(e => e.Location)
                  .HasColumnName("location")
                  .HasMaxLength(150);

            entity.Property(e => e.DashboardStatus)
                  .HasColumnName("dashboard_status")
                  .HasMaxLength(20);

            entity.Property(e => e.ActualStatus)
                  .HasColumnName("actual_status")
                  .HasMaxLength(20);

            entity.Property(e => e.OperatorName)
                  .HasColumnName("operator_name")
                  .HasMaxLength(100);

            entity.Property(e => e.OperatorMobile)
                  .HasColumnName("operator_mobile")
                  .HasMaxLength(20);

            entity.Property(e => e.JeName)
                  .HasColumnName("je_name")
                  .HasMaxLength(100);

            entity.Property(e => e.JeMobile)
                  .HasColumnName("je_mobile")
                  .HasMaxLength(20);

            entity.Property(e => e.ComplainantName)
                  .HasColumnName("complainant_name")
                  .HasMaxLength(100);

            entity.Property(e => e.ComplainantMobile)
                  .HasColumnName("complainant_mobile")
                  .HasMaxLength(20);

            entity.Property(e => e.Status)
                  .HasColumnName("status")
                  .HasMaxLength(20)
                  .HasDefaultValue("OPEN");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("idx_complaint_log_status");

            entity.Property(e => e.Remarks)
                  .HasColumnName("remarks")
                  .HasMaxLength(500);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            entity.HasOne(e => e.PumpMaster)
                  .WithMany()
                  .HasForeignKey(e => e.PumpId)
                  .HasConstraintName("fk_complaint_log_pump")
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
