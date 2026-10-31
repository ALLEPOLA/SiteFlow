using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data.Configurations;

public class MaterialIssueLineConfiguration
    : IEntityTypeConfiguration<MaterialIssueLine>
{
    public void Configure(EntityTypeBuilder<MaterialIssueLine> entity)
    {
        entity.HasKey(l => l.Id);

        entity.Property(l => l.Quantity)
            .HasPrecision(18, 3);

        entity.Property(l => l.UnitCost)
            .HasPrecision(18, 2);

        entity.HasOne(l => l.Material)
            .WithMany()
            .HasForeignKey(l => l.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(l => new
        {
            l.MaterialIssueId,
            l.MaterialId
        })
        .IsUnique();

        entity.ToTable("MaterialIssueLines", table =>
        {
            table.HasCheckConstraint(
                "CK_MaterialIssueLines_Quantity",
                "`Quantity` > 0");

            table.HasCheckConstraint(
                "CK_MaterialIssueLines_UnitCost",
                "`UnitCost` >= 0");
        });
    }
}