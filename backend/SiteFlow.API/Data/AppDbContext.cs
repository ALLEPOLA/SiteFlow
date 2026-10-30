using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Material> Materials => Set<Material>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}