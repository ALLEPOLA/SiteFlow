using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Models;
namespace SiteFlow.API.Data.Configurations;

public static class StorageConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Store>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Code).HasMaxLength(30).IsRequired();
            e.Property(s => s.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(s => new { s.ProjectId, s.Code }).IsUnique();
            e.HasOne(s => s.Project).WithMany().HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StorageLocation>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Code).HasMaxLength(30).IsRequired();
            e.Property(l => l.Name).HasMaxLength(150).IsRequired();
            e.Property(l => l.Rack).HasMaxLength(60).IsRequired();
            e.Property(l => l.Bin).HasMaxLength(60).IsRequired();
            e.HasIndex(l => new { l.StoreId, l.Code }).IsUnique();
            e.HasOne(l => l.Store).WithMany(s => s.Locations).HasForeignKey(l => l.StoreId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockMovement>().HasOne(m => m.StorageLocation).WithMany().HasForeignKey(m => m.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockMovement>().HasIndex(m => new { m.StorageLocationId, m.MaterialId });
        b.Entity<MaterialReservation>().HasOne(r => r.StorageLocation).WithMany().HasForeignKey(r => r.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaterialReservation>().HasIndex(r => new { r.StorageLocationId, r.MaterialId });
        b.Entity<GoodsReceipt>().HasOne(r => r.StorageLocation).WithMany().HasForeignKey(r => r.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaterialIssue>().HasOne(i => i.StorageLocation).WithMany().HasForeignKey(i => i.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryOperation>().HasOne(o => o.StorageLocation).WithMany().HasForeignKey(o => o.StorageLocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryOperation>().HasOne(o => o.DestinationStorageLocation).WithMany().HasForeignKey(o => o.DestinationStorageLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
