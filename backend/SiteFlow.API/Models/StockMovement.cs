namespace SiteFlow.API.Models;

public class StockMovement
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    public string MovementType { get; set; } = "Receipt";

    // Receipt: positive.
    // Issue: negative.
    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateOnly MovementDate { get; set; }

    public int? GoodsReceiptLineId { get; set; }

    public GoodsReceiptLine? GoodsReceiptLine { get; set; }

    public int? MaterialIssueLineId { get; set; }

    public MaterialIssueLine? MaterialIssueLine { get; set; }

    public string CreatedByUserId { get; set; } = "";

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}