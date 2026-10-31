using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();
        });

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
}