namespace SiteFlow.API.Models;

public class GoodsReceipt
{
    public int Id { get; set; }

    // Identifies a submission so retrying it does not
    // accidentally record the same delivery twice.
    public string ClientRequestId { get; set; } = string.Empty;

    public int PurchaseOrderId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public DateOnly ReceivedDate { get; set; }

    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = "Posted";

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<GoodsReceiptLine> Lines { get; set; } = new();
}