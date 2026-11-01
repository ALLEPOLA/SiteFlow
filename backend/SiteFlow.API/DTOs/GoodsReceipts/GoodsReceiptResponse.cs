namespace SiteFlow.API.DTOs.GoodsReceipts;

public class GoodsReceiptResponse
{
    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = "";

    public int ProjectId { get; set; }
    public int Id { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public string ClientRequestId { get; set; } = string.Empty;

    public int PurchaseOrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public DateOnly ReceivedDate { get; set; }

    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public decimal TotalValue { get; set; }

    public List<GoodsReceiptLineResponse> Lines { get; set; } = new();
}

public class GoodsReceiptLineResponse
{
    public int MaterialId { get; set; }
    public int Id { get; set; }

    public int PurchaseOrderLineId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }
}

public class ReceivableOrderResponse
{
    public int ProjectId { get; set; }
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<ReceivableOrderLineResponse> Lines { get; set; } = new();
}

public class ReceivableOrderLineResponse
{
    public int PurchaseOrderLineId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public decimal UnitCost { get; set; }
}
