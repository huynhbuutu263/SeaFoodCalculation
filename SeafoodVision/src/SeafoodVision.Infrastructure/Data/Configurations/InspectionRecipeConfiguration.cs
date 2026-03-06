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

        // Use the property selector (not the backing-field string) so EF does not
        // create a second navigation that conflicts with the auto-detected "_rois" field.
        builder.HasMany(r => r.RoiDefinitions)
            .WithOne()
            .HasForeignKey(roi => roi.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
