namespace SiteFlow.API.Models;

public class MaterialReservation
{
    public int Id { get; set; }
    public string ClientRequestId { get; set; } = "";
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int MaterialRequestId { get; set; }
    public MaterialRequest MaterialRequest { get; set; } = null!;
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public string Reason { get; set; } = "";
    public string ReleaseReason { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
}
