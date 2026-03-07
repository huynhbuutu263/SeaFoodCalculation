using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="InspectionRecipe"/>.
/// Maps to table <c>inspection_recipes</c>.
/// </summary>
internal sealed class InspectionRecipeConfiguration : IEntityTypeConfiguration<InspectionRecipe>
{
    public void Configure(EntityTypeBuilder<InspectionRecipe> builder)
    {
        builder.ToTable("inspection_recipes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.CameraId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.IsActive)
            .IsRequired();

        builder.Property(r => r.ExecutionMode)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => new { r.CameraId, r.IsActive });

        // Tell EF Core to use the private backing field "_rois" when populating this
        // navigation, because the public property returns AsReadOnly() which is not
        // writable and would throw NotSupportedException at materialisation time.
        builder.Navigation(r => r.RoiDefinitions)
            .HasField("_rois")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.RoiDefinitions)
            .WithOne()
            .HasForeignKey(roi => roi.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
