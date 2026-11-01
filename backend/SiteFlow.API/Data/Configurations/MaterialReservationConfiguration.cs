using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data.Configurations;

public class MaterialReservationConfiguration : IEntityTypeConfiguration<MaterialReservation>
{
    public void Configure(EntityTypeBuilder<MaterialReservation> entity)
    {
        entity.HasKey(r => r.Id);
        entity.Property(r => r.ClientRequestId).HasMaxLength(36).IsRequired();
        entity.HasIndex(r => r.ClientRequestId).IsUnique();
        entity.HasIndex(r => new { r.ProjectId, r.MaterialId });
        entity.Property(r => r.Quantity).HasPrecision(18, 3);
        entity.Property(r => r.ConsumedQuantity).HasPrecision(18, 3);
        entity.Property(r => r.ReleasedQuantity).HasPrecision(18, 3);
        entity.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        entity.Property(r => r.ReleaseReason).HasMaxLength(1000).IsRequired();
        entity.Property(r => r.CreatedByUserId).HasMaxLength(255).IsRequired();
        entity.HasOne(r => r.Project).WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(r => r.MaterialRequest).WithMany().HasForeignKey(r => r.MaterialRequestId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(r => r.Material).WithMany().HasForeignKey(r => r.MaterialId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(r => r.CreatedByUser).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable("MaterialReservations", table => table.HasCheckConstraint("CK_MaterialReservations_Quantities",
            "`Quantity` > 0 AND `ConsumedQuantity` >= 0 AND `ReleasedQuantity` >= 0 AND `ConsumedQuantity` + `ReleasedQuantity` <= `Quantity`"));
    }
}
