namespace SiteFlow.API.Models;

public class PurchaseOrder
{
    public int Id { get; set; }

    public int MaterialRequestId { get; set; }

    public MaterialRequest MaterialRequest { get; set; } = null!;

    public int SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public DateOnly OrderDate { get; set; }

    public DateOnly ExpectedDeliveryDate { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<PurchaseOrderLine> Lines { get; set; } = new();
}