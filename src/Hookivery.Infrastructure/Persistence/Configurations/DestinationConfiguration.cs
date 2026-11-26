using System.Globalization;

using Hookivery.Domain.Destinations;
using Hookivery.Domain.Sources;
using Hookivery.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hookivery.Infrastructure.Persistence.Configurations;

internal sealed class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.ToTable("destinations", table =>
        {
            table.HasCheckConstraint(
                "ck_destinations_max_attempts",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"max_attempts BETWEEN {DeliverySettings.MinAttempts} AND {DeliverySettings.MaxAttemptsLimit}"));

            table.HasCheckConstraint(
                "ck_destinations_timeout",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"timeout BETWEEN interval '{(int)DeliverySettings.MinTimeout.TotalSeconds} seconds' AND interval '{(int)DeliverySettings.MaxTimeout.TotalSeconds} seconds'"));
        });

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name).HasMaxLength(Destination.MaxNameLength).IsRequired();

        builder.Property(d => d.Url)
            .HasConversion(url => url.AbsoluteUri, value => new Uri(value, UriKind.Absolute))
            .HasMaxLength(Destination.MaxUrlLength)
            .IsRequired();

        builder.Property(d => d.Secret)
            .HasConversion<SigningSecretConverter>()
            .HasMaxLength(SigningSecretConverter.MaxLength)
            .IsRequired();

        builder.Property(d => d.Timeout).IsRequired();
        builder.Property(d => d.MaxAttempts).IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();

        builder.PrimitiveCollection(d => d.EventTypes)
            .HasField("_eventTypes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("event_types")
            .IsRequired();

        builder.HasOne<Source>()
            .WithMany()
            .HasForeignKey(d => d.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.SourceId).HasDatabaseName("ix_destinations_source_id");
    }
}
