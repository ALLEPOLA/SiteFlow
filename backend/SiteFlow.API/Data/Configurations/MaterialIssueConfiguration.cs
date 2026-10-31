using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteFlow.API.Models;

namespace SiteFlow.API.Data.Configurations;

public class MaterialIssueConfiguration
    : IEntityTypeConfiguration<MaterialIssue>
{
    public void Configure(EntityTypeBuilder<MaterialIssue> entity)
    {
        entity.ToTable("MaterialIssues");

        entity.HasKey(i => i.Id);

        entity.Property(i => i.ClientRequestId)
            .HasMaxLength(36)
            .IsRequired();

        entity.HasIndex(i => i.ClientRequestId)
            .IsUnique();

        entity.Property(i => i.IssueDate)
            .HasColumnType("date")
            .IsRequired();

        entity.Property(i => i.Purpose)
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(i => i.ReceivedBy)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(i => i.Notes)
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(i => i.Status)
            .HasMaxLength(20)
            .IsRequired();

        entity.Property(i => i.CreatedByUserId)
            .HasMaxLength(255)
            .IsRequired();

        entity.Property(i => i.CreatedAtUtc)
            .HasColumnType("datetime(6)")
            .IsRequired();

        entity.HasOne(i => i.Project)
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(i => i.CreatedByUser)
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasMany(i => i.Lines)
            .WithOne(l => l.MaterialIssue)
            .HasForeignKey(l => l.MaterialIssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}