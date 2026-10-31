namespace SiteFlow.API.Models;

public class GoodsReceiptLine
{
    public int Id { get; set; }

    public int GoodsReceiptId { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public int PurchaseOrderLineId { get; set; }

    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;

    public decimal Quantity { get; set; }

    // Copied by the backend from the purchase order price.
    public decimal UnitCost { get; set; }
}