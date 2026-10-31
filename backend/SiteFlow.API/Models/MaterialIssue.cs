namespace SiteFlow.API.Models;

public class MaterialIssue
{
    public int Id { get; set; }

    public string ClientRequestId { get; set; } = "";

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public DateOnly IssueDate { get; set; }

    public string Purpose { get; set; } = "";

    public string ReceivedBy { get; set; } = "";

    public string Notes { get; set; } = "";

    public string Status { get; set; } = "Posted";

    public string CreatedByUserId { get; set; } = "";

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MaterialIssueLine> Lines { get; set; } = new();
}