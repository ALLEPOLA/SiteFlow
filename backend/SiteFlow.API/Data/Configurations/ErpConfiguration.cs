using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data.Configurations;

public static class ErpConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<ApplicationUser>().Property(u => u.IsActive).HasDefaultValue(true);
        b.Entity<Material>().Property(m => m.ReorderLevel).HasPrecision(18, 3);
        b.Entity<ProjectAssignment>(e =>
        {
            e.HasKey(a => new { a.ProjectId, a.UserId });
            e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditEntry>(e =>
        {
            e.Property(a => a.UserId).HasMaxLength(255);
            e.Property(a => a.ActorName).HasMaxLength(254);
            e.Property(a => a.Action).HasMaxLength(150);
            e.Property(a => a.Resource).HasMaxLength(300);
            e.Property(a => a.Reason).HasMaxLength(1000);
            e.HasIndex(a => a.CreatedAtUtc);
        });
        b.Entity<InventoryOperation>(e =>
        {
            e.Property(o => o.RequestHash).HasMaxLength(64);
            e.Property(o => o.ClientRequestId).HasMaxLength(36);
            e.HasIndex(o => o.ClientRequestId).IsUnique();
            e.Property(o => o.Kind).HasMaxLength(30);
            e.Property(o => o.Status).HasMaxLength(20).HasDefaultValue("Posted");
            e.Property(o => o.Quantity).HasPrecision(18, 3);
            e.Property(o => o.UnitCost).HasPrecision(18, 2);
            e.Property(o => o.Reason).HasMaxLength(1000);
            e.Property(o => o.CreatedByUserId).HasMaxLength(255);
            e.HasOne(o => o.Project).WithMany().HasForeignKey(o => o.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.DestinationProject).WithMany().HasForeignKey(o => o.DestinationProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Material).WithMany().HasForeignKey(o => o.MaterialId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.GoodsReceiptLine).WithMany().HasForeignKey(o => o.GoodsReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockMovement>(e =>
        {
            e.HasOne(m => m.InventoryOperation).WithMany().HasForeignKey(m => m.InventoryOperationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.ReversesMovement).WithMany().HasForeignKey(m => m.ReversesMovementId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => m.ReversesMovementId).IsUnique();
        });
        b.Entity<SupplierInvoice>(e =>
        {
            e.Property(i => i.ClientRequestId).HasMaxLength(36);
            e.HasIndex(i => i.ClientRequestId).IsUnique();
            e.Property(i => i.Number).HasMaxLength(100);
            e.HasIndex(i => new { i.SupplierId, i.Number }).IsUnique();
            e.Property(i => i.Status).HasMaxLength(20);
            e.Property(i => i.CreatedByUserId).HasMaxLength(255);
            e.HasOne(i => i.PurchaseOrder).WithMany().HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierInvoiceLine>(e =>
        {
            e.Property(l => l.Quantity).HasPrecision(18, 3);
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
            e.HasIndex(l => new { l.SupplierInvoiceId, l.GoodsReceiptLineId }).IsUnique();
            e.HasOne(l => l.GoodsReceiptLine).WithMany().HasForeignKey(l => l.GoodsReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierPayment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.ClientRequestId).HasMaxLength(36);
            e.HasIndex(p => p.ClientRequestId).IsUnique();
            e.Property(p => p.Reference).HasMaxLength(150);
            e.Property(p => p.CreatedByUserId).HasMaxLength(255);
        });
        b.Entity<DocumentAttachment>(e =>
        {
            e.Property(a => a.ResourceType).HasMaxLength(30);
            e.Property(a => a.FileName).HasMaxLength(200);
            e.Property(a => a.ContentType).HasMaxLength(100);
            e.Property(a => a.UploadedByUserId).HasMaxLength(255);
            e.HasOne(a => a.Project).WithMany().HasForeignKey(a => a.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.ResourceType, a.ResourceId });
        });
        b.Entity<NotificationRead>(e =>
        {
            e.Property(n => n.UserId).HasMaxLength(255);
            e.Property(n => n.Key).HasMaxLength(100);
            e.HasKey(n => new { n.UserId, n.Key });
        });
    }
}
