using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="RoiDefinition"/>.
/// Maps to table <c>roi_definitions</c>.
/// The <see cref="RegionOfInterest"/> value object is split into two shadow columns:
/// <c>region_type</c> (byte) and <c>points_json</c> (JSON array of {x,y}).
/// </summary>
internal sealed class RoiDefinitionConfiguration : IEntityTypeConfiguration<RoiDefinition>
{
    public void Configure(EntityTypeBuilder<RoiDefinition> builder)
    {
        builder.ToTable("roi_definitions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Order)
            .HasColumnName("roi_order")
            .IsRequired();

        builder.Property(r => r.RecipeId)
            .IsRequired();

        // ── RegionOfInterest value object ─────────────────────────────────────
        // Stored as two shadow columns so we avoid complex owned-type mapping.
        // RecipeRepository serialises/deserialises these manually.

        builder.Property<byte>("RegionTypeRaw")
            .HasColumnName("region_type")
            .IsRequired();

        builder.Property<string>("PointsJson")
            .HasColumnName("points_json")
            .HasColumnType("json")
            .IsRequired();

        // Region is handled via shadow columns above — tell EF to ignore the CLR property.
        builder.Ignore(r => r.Region);

        // Tell EF Core to use the private backing field "_steps" when populating this
        // navigation, because the public property returns AsReadOnly() which is not
        // writable and would throw NotSupportedException at materialisation time.
        builder.Navigation(r => r.Steps)
            .HasField("_steps")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Steps)
            .WithOne()
            .HasForeignKey(s => s.RoiDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
