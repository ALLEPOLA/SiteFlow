using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.GoodsReceipts;

public class CreateGoodsReceiptDto
{
    [Range(1, int.MaxValue)] public int? StorageLocationId { get; set; }

    [Required]
    [StringLength(36)]
    public string ClientRequestId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PurchaseOrderId { get; set; }

    public DateOnly ReceivedDate { get; set; }

    [Required]
    [StringLength(100)]
    public string DeliveryNoteNumber { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new();
}

public class CreateGoodsReceiptLineDto
{
    [Range(1, int.MaxValue)]
    public int PurchaseOrderLineId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999.999")]
    public decimal Quantity { get; set; }
}