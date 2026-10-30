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

    public List<MaterialRequestLine> Lines { get; set; } = new();
}