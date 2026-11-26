using Hookivery.Domain.Events;
using Hookivery.Domain.Sources;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hookivery.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events", table =>
            table.HasCheckConstraint("ck_events_status", CheckConstraints.EnumValues<EventStatus>("status")));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.ExternalId).HasMaxLength(Event.MaxExternalIdLength).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(EventTypeName.MaxLength).IsRequired();

        builder.Property(e => e.Payload).HasColumnType("text").IsRequired();

        builder.Property(e => e.ReceivedAt).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne<Source>()
            .WithMany()
            .HasForeignKey(e => e.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SourceId, e.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_events_source_id_external_id");

        builder.HasIndex(e => e.ReceivedAt)
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_events_pending");
    }
}
