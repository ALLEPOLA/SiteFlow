namespace SiteFlow.API.Models;

public class PurchaseOrderLine
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int MaterialRequestLineId { get; set; }

    public MaterialRequestLine MaterialRequestLine { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}