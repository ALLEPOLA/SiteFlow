namespace SiteFlow.API.Models;

public class MaterialRequest
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public DateOnly RequiredDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public string? ReviewedByUserId { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewComment { get; set; }

    public List<MaterialRequestLine> Lines { get; set; } = new();
}