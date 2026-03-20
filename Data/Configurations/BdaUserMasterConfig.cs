using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class BdaUserMasterConfig : IEntityTypeConfiguration<BdaUserMaster>
    {
        public void Configure(EntityTypeBuilder<BdaUserMaster> entity)
        {
            entity.ToTable("bda_user_master_tbl");

            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId)
                  .HasColumnName("user_id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                  .HasColumnName("name")
                  .HasMaxLength(100);

            entity.Property(e => e.Username)
                  .HasColumnName("username")
                  .HasMaxLength(50);

            // Map enum to string and specify MySQL enum column type
            entity.Property(e => e.UserType)
                  .HasColumnName("user_type")
                  .HasConversion<string>()
                  .HasColumnType("enum('ADMIN','OPERATOR','BDA_OFFICIAL')")
                  .IsUnicode(false);

            entity.Property(e => e.Password)
                  .HasColumnName("password")
                  .HasMaxLength(255);

            entity.Property(e => e.MobileNumber)
                  .HasColumnName("mobile_number")
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
        }
    }
}