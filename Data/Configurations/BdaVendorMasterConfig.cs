using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class BdaVendorMasterConfig : IEntityTypeConfiguration<BdaVendorMaster>
    {
        public void Configure(EntityTypeBuilder<BdaVendorMaster> entity)
        {
            entity.ToTable("bda_vendor_master_tbl");

            entity.HasKey(e => e.VendorId);

            entity.Property(e => e.VendorId)
                  .HasColumnName("vendor_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.VendorName)
                  .HasColumnName("vendor_name")
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ContactMobile)
                  .HasColumnName("contact_mobile")
                  .HasMaxLength(15);

            entity.Property(e => e.IsActive)
                  .HasColumnName("is_active")
                  .HasColumnType("tinyint(1)")
                  .HasDefaultValue(true);

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.VendorName)
                  .IsUnique()
                  .HasDatabaseName("uq_vendor_name");
        }
    }
}
