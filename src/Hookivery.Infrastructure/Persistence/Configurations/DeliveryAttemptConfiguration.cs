using Hookivery.Domain.Deliveries;
using Hookivery.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hookivery.Infrastructure;

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("delivery_attempts", table =>
        {
            table.HasCheckConstraint("ck_delivery_attempts_outcome", CheckConstraints.EnumValues<AttemptOutcome>("outcome"));
            table.HasCheckConstraint("ck_delivery_attempts_attempt_number", "attempt_number >= 1");
            table.HasCheckConstraint("ck_delivery_attempts_status_code", "status_code IS NULL OR status_code BETWEEN 100 AND 599");
            table.HasCheckConstraint("ck_delivery_attempts_duration", "duration >= interval '0'");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.Duration).IsRequired();
        builder.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.Error).HasMaxLength(DeliveryAttempt.MaxErrorLength);
        builder.Property(a => a.ResponseBody).HasMaxLength(DeliveryAttempt.MaxResponseBodyLength);

        builder.HasOne<Delivery>()
            .WithMany()
            .HasForeignKey(a => a.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.DeliveryId, a.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("ux_delivery_attempts_delivery_id_attempt_number");
    }
}
