namespace SiteFlow.API.Models;

public class ProjectAssignment
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
}

public class AuditEntry
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class InventoryOperation
{
    public int StorageLocationId { get; set; }
    public StorageLocation StorageLocation { get; set; } = null!;
    public int? DestinationStorageLocationId { get; set; }
    public StorageLocation? DestinationStorageLocation { get; set; }
    public int Id { get; set; }
    public string ClientRequestId { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int? DestinationProjectId { get; set; }
    public Project? DestinationProject { get; set; }
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "Posted";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? GoodsReceiptLineId { get; set; }
    public GoodsReceiptLine? GoodsReceiptLine { get; set; }
}

public class SupplierInvoice
{
    public int Id { get; set; }
    public string ClientRequestId { get; set; } = "";
    public int SupplierId { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public string Number { get; set; } = "";
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "Posted";
    public string CreatedByUserId { get; set; } = "";
    public List<SupplierInvoiceLine> Lines { get; set; } = new();
    public List<SupplierPayment> Payments { get; set; } = new();
}

public class SupplierInvoiceLine
{
    public int Id { get; set; }
    public int SupplierInvoiceId { get; set; }
    public SupplierInvoice SupplierInvoice { get; set; } = null!;
    public int GoodsReceiptLineId { get; set; }
    public GoodsReceiptLine GoodsReceiptLine { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class SupplierPayment
{
    public int Id { get; set; }
    public int SupplierInvoiceId { get; set; }
    public SupplierInvoice SupplierInvoice { get; set; } = null!;
    public string ClientRequestId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string Reference { get; set; } = "";
    public string CreatedByUserId { get; set; } = "";
}

public class DocumentAttachment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string ResourceType { get; set; } = "";
    public int ResourceId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string UploadedByUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class NotificationRead
{
    public string UserId { get; set; } = "";
    public string Key { get; set; } = "";
}
