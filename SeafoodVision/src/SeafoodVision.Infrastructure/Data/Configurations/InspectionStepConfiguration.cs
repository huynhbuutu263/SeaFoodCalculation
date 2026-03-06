using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeafoodVision.Domain.Entities;

namespace SeafoodVision.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="InspectionStep"/>.
/// Maps to table <c>inspection_steps</c>.
/// </summary>
internal sealed class InspectionStepConfiguration : IEntityTypeConfiguration<InspectionStep>
{
    public void Configure(EntityTypeBuilder<InspectionStep> builder)
    {
        builder.ToTable("inspection_steps");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Order)
            .HasColumnName("step_order")
            .IsRequired();

        builder.Property(s => s.StepType)
            .HasConversion<short>()
            .HasColumnName("step_type")
            .IsRequired();

        builder.Property(s => s.ParametersJson)
            .HasColumnName("parameters_json")
            .HasColumnType("json")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.RoiDefinitionId)
            .HasColumnName("roi_id")
            .IsRequired();
    }
}
