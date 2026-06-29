using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    public class PushSubscriptionConfig : IEntityTypeConfiguration<PushSubscription>
    {
        public void Configure(EntityTypeBuilder<PushSubscription> entity)
        {
            entity.ToTable("push_subscription_tbl");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.Property(e => e.Mobile)
                  .HasColumnName("mobile")
                  .HasMaxLength(15);

            entity.Property(e => e.UserType)
                  .HasColumnName("user_type")
                  .HasMaxLength(20);

            entity.Property(e => e.Endpoint)
                  .HasColumnName("endpoint")
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.P256dh)
                  .HasColumnName("p256dh")
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.Auth)
                  .HasColumnName("auth")
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.RowInsertionDateTime)
                  .HasColumnName("row_insertion_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.RowUpdationDateTime)
                  .HasColumnName("row_updation_date_time")
                  .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Endpoint)
                  .IsUnique()
                  .HasDatabaseName("uq_push_endpoint");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("idx_push_user");
        }
    }
}
