using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.PurchaseOrders;

public class CreatePurchaseOrderDto
{
    [Range(1, int.MaxValue)]
    public int MaterialRequestId { get; set; }

    [Range(1, int.MaxValue)]
    public int SupplierId { get; set; }

    public DateOnly OrderDate { get; set; }

    public DateOnly ExpectedDeliveryDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreatePurchaseOrderLineDto> Lines { get; set; } = new();
}

public class CreatePurchaseOrderLineDto
{
    [Range(1, int.MaxValue)]
    public int MaterialRequestLineId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999.999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal UnitPrice { get; set; }
}