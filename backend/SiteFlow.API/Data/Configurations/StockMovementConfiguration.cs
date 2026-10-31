using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data.Configurations;

public class StockMovementConfiguration
    : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> entity)
    {
        entity.HasKey(m => m.Id);

        entity.Property(m => m.MovementType)
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(m => m.Quantity)
            .HasPrecision(18, 3);

        entity.Property(m => m.UnitCost)
            .HasPrecision(18, 2);

        entity.Property(m => m.MovementDate)
            .HasColumnType("date")
            .IsRequired();

        entity.Property(m => m.CreatedByUserId)
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(m => m.CreatedAtUtc)
            .HasColumnType("datetime(6)")
            .IsRequired();

        entity.HasOne(m => m.Project)
            .WithMany()
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(m => m.Material)
            .WithMany()
            .HasForeignKey(m => m.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(m => m.GoodsReceiptLine)
            .WithMany()
            .HasForeignKey(m => m.GoodsReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(m => m.MaterialIssueLine)
            .WithMany()
            .HasForeignKey(m => m.MaterialIssueLineId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(m => m.CreatedByUser)
            .WithMany()
            .HasForeignKey(m => m.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(m => m.GoodsReceiptLineId)
            .IsUnique();

        entity.HasIndex(m => m.MaterialIssueLineId)
            .IsUnique();

        entity.HasIndex(m => new
        {
            m.ProjectId,
            m.MaterialId
        });

        entity.HasIndex(m => m.MovementDate);

        entity.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_StockMovements_TypeQuantity",
                """
                (
                    `MovementType` = 'Receipt'
                    AND `Quantity` > 0
                    AND `GoodsReceiptLineId` IS NOT NULL
                    AND `MaterialIssueLineId` IS NULL
                )
                OR
                (
                    `MovementType` = 'Issue'
                    AND `Quantity` < 0
                    AND `GoodsReceiptLineId` IS NULL
                    AND `MaterialIssueLineId` IS NOT NULL
                )
                """);

            table.HasCheckConstraint(
                "CK_StockMovements_UnitCost",
                "`UnitCost` >= 0");
        });
    }
}