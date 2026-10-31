namespace SiteFlow.API.DTOs.PurchaseOrders;

public class PurchaseOrderResponse
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public int MaterialRequestId { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public int SupplierId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateOnly ExpectedDeliveryDate { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public decimal TotalAmount { get; set; }

    public List<PurchaseOrderLineResponse> Lines { get; set; } = new();
}

public class PurchaseOrderLineResponse
{
    public int Id { get; set; }

    public int MaterialRequestLineId { get; set; }

    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public class AvailableRequestResponse
{
    public int Id { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public DateOnly RequiredDate { get; set; }

    public List<AvailableRequestLineResponse> Lines { get; set; } = new();
}

public class AvailableRequestLineResponse
{
    public int MaterialRequestLineId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal RequestedQuantity { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public decimal EstimatedUnitPrice { get; set; }
}