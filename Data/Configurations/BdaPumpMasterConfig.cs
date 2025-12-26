using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class BdaPumpMasterConfig : IEntityTypeConfiguration<BdaPumpMaster>
    {
        public void Configure(EntityTypeBuilder<BdaPumpMaster> entity)
        {
            entity.ToTable("bda_pump_master_tbl");

            entity.HasKey(e => e.PumpId);

            entity.Property(e => e.PumpId)
                  .HasColumnName("pump_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.VendorName)
                  .HasColumnName("vendor_name")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Category)
                  .HasColumnName("category")
                  .HasMaxLength(50);

            entity.Property(e => e.IsActive)
                  .HasColumnName("is_active")
                  .HasColumnType("tinyint(1)")
                  .HasDefaultValue(true);

            entity.Property(e => e.RowActionCount)
                  .HasColumnName("row_action_count")
                  .HasDefaultValue(1);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        }
    }
}