using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data.Configurations;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Material> Materials => Set<Material>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<MaterialRequest> MaterialRequests =>
        Set<MaterialRequest>();

    public DbSet<MaterialRequestLine> MaterialRequestLines =>
        Set<MaterialRequestLine>();

    public DbSet<PurchaseOrder> PurchaseOrders =>
        Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines =>
        Set<PurchaseOrderLine>();

    public DbSet<GoodsReceipt> GoodsReceipts =>
        Set<GoodsReceipt>();

    public DbSet<GoodsReceiptLine> GoodsReceiptLines =>
        Set<GoodsReceiptLine>();

    public DbSet<StockMovement> StockMovements =>
        Set<StockMovement>();

    public DbSet<MaterialIssue> MaterialIssues =>
        Set<MaterialIssue>();

    public DbSet<MaterialIssueLine> MaterialIssueLines =>
        Set<MaterialIssueLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureProjects(modelBuilder);
        ConfigureMaterials(modelBuilder);
        ConfigureSuppliers(modelBuilder);
        ConfigureMaterialRequests(modelBuilder);
        ConfigureMaterialRequestLines(modelBuilder);
        ConfigurePurchaseOrders(modelBuilder);
        ConfigurePurchaseOrderLines(modelBuilder);
        ConfigureGoodsReceipts(modelBuilder);
        ConfigureGoodsReceiptLines(modelBuilder);

        modelBuilder.ApplyConfiguration(
            new StockMovementConfiguration());

        modelBuilder.ApplyConfiguration(
            new MaterialIssueConfiguration());

        modelBuilder.ApplyConfiguration(
            new MaterialIssueLineConfiguration());
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(u => u.MustChangePassword)
                .IsRequired();
        });
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Code)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(p => p.Code)
                .IsUnique();

            entity.Property(p => p.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.Location)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.Budget)
                .HasPrecision(18, 2);

            entity.Property(p => p.Status)
                .HasMaxLength(20)
                .IsRequired();
        });
    }

    private static void ConfigureMaterials(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Code)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(m => m.Code)
                .IsUnique();

            entity.Property(m => m.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(m => m.Category)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(m => m.Unit)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(m => m.IsActive)
                .IsRequired();
        });
    }

    private static void ConfigureSuppliers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Code)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(s => s.Code)
                .IsUnique();

            entity.Property(s => s.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(s => s.ContactPerson)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(s => s.Phone)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(s => s.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(s => s.Address)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(s => s.IsActive)
                .IsRequired();
        });
    }

    private static void ConfigureMaterialRequests(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.RequiredDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(r => r.Reason)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(r => r.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(r => r.CreatedAtUtc)
                .HasColumnType("datetime(6)")
                .IsRequired();

            entity.Property(r => r.CreatedByUserId)
                .HasMaxLength(255);

            entity.Property(r => r.SubmittedAtUtc)
                .HasColumnType("datetime(6)");

            entity.Property(r => r.ReviewedByUserId)
                .HasMaxLength(255);

            entity.Property(r => r.ReviewedAtUtc)
                .HasColumnType("datetime(6)");

            entity.Property(r => r.ReviewComment)
                .HasMaxLength(1000);

            entity.HasOne(r => r.Project)
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReviewedByUser)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Lines)
                .WithOne(l => l.MaterialRequest)
                .HasForeignKey(l => l.MaterialRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMaterialRequestLines(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaterialRequestLine>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Quantity)
                .HasPrecision(18, 3);

            entity.Property(l => l.EstimatedUnitPrice)
                .HasPrecision(18, 2);

            entity.HasOne(l => l.Material)
                .WithMany()
                .HasForeignKey(l => l.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(l => new
            {
                l.MaterialRequestId,
                l.MaterialId
            })
            .IsUnique();

            entity.ToTable("MaterialRequestLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_MaterialRequestLines_Quantity",
                    "`Quantity` > 0");

                table.HasCheckConstraint(
                    "CK_MaterialRequestLines_EstimatedUnitPrice",
                    "`EstimatedUnitPrice` >= 0");
            });
        });
    }

    private static void ConfigurePurchaseOrders(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.OrderDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(p => p.ExpectedDeliveryDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(p => p.Notes)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(p => p.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(p => p.CreatedByUserId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(p => p.CreatedAtUtc)
                .HasColumnType("datetime(6)")
                .IsRequired();

            entity.Property(p => p.IssuedByUserId)
                .HasMaxLength(255);

            entity.Property(p => p.IssuedAtUtc)
                .HasColumnType("datetime(6)");

            entity.HasOne(p => p.MaterialRequest)
                .WithMany()
                .HasForeignKey(p => p.MaterialRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.IssuedByUser)
                .WithMany()
                .HasForeignKey(p => p.IssuedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Lines)
                .WithOne(l => l.PurchaseOrder)
                .HasForeignKey(l => l.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable("PurchaseOrders", table =>
            {
                table.HasCheckConstraint(
                    "CK_PurchaseOrders_DeliveryDate",
                    "`ExpectedDeliveryDate` >= `OrderDate`");
            });
        });
    }

    private static void ConfigurePurchaseOrderLines(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Quantity)
                .HasPrecision(18, 3);

            entity.Property(l => l.UnitPrice)
                .HasPrecision(18, 2);

            entity.HasOne(l => l.MaterialRequestLine)
                .WithMany()
                .HasForeignKey(l => l.MaterialRequestLineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(l => new
            {
                l.PurchaseOrderId,
                l.MaterialRequestLineId
            })
            .IsUnique();

            entity.ToTable("PurchaseOrderLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_PurchaseOrderLines_Quantity",
                    "`Quantity` > 0");

                table.HasCheckConstraint(
                    "CK_PurchaseOrderLines_UnitPrice",
                    "`UnitPrice` >= 0");
            });
        });
    }

    private static void ConfigureGoodsReceipts(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.ClientRequestId)
                .HasMaxLength(36)
                .IsRequired();

            entity.HasIndex(r => r.ClientRequestId)
                .IsUnique();

            entity.Property(r => r.ReceivedDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(r => r.DeliveryNoteNumber)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(r => r.Notes)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(r => r.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(r => r.CreatedByUserId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(r => r.CreatedAtUtc)
                .HasColumnType("datetime(6)")
                .IsRequired();

            entity.HasOne(r => r.PurchaseOrder)
                .WithMany()
                .HasForeignKey(r => r.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedByUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Lines)
                .WithOne(l => l.GoodsReceipt)
                .HasForeignKey(l => l.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGoodsReceiptLines(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Quantity)
                .HasPrecision(18, 3);

            entity.Property(l => l.UnitCost)
                .HasPrecision(18, 2);

            entity.HasOne(l => l.PurchaseOrderLine)
                .WithMany()
                .HasForeignKey(l => l.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(l => new
            {
                l.GoodsReceiptId,
                l.PurchaseOrderLineId
            })
            .IsUnique();

            entity.ToTable("GoodsReceiptLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_GoodsReceiptLines_Quantity",
                    "`Quantity` > 0");

                table.HasCheckConstraint(
                    "CK_GoodsReceiptLines_UnitCost",
                    "`UnitCost` >= 0");
            });
        });
    }
}