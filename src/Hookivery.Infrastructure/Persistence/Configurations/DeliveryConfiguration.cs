using Hookivery.Domain.Deliveries;
using Hookivery.Domain.Destinations;
using Hookivery.Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hookivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public const int MaxWorkerIdLength = 200;

    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries", table =>
        {
            table.HasCheckConstraint("ck_deliveries_status", CheckConstraints.EnumValues<DeliveryStatus>("status"));
            table.HasCheckConstraint("ck_deliveries_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint("ck_deliveries_claim_count", "claim_count >= 0");
            table.HasCheckConstraint("ck_deliveries_claim_pair", "(claimed_by IS NULL) = (claimed_until IS NULL)");
        });

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.AttemptCount).IsRequired();
        builder.Property(d => d.NextAttemptAt).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.LastError).HasMaxLength(Delivery.MaxErrorLength);
        builder.Property(d => d.ClaimedBy).HasMaxLength(MaxWorkerIdLength);
        builder.Property(d => d.ClaimCount).IsRequired();

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Destination>()
            .WithMany()
            .HasForeignKey(d => d.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.EventId, d.DestinationId })
            .IsUnique()
            .HasDatabaseName("ux_deliveries_event_id_destination_id");

        builder.HasIndex(d => d.NextAttemptAt)
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_deliveries_claimable");

        builder.HasIndex(d => d.DestinationId).HasDatabaseName("ix_deliveries_destination_id");
    }
}