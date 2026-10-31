using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize(Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer), Route("api/supplier-accounts")]
public class SupplierAccountsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var invoices = await db.SupplierInvoices.AsNoTracking().Include(i => i.Lines).Include(i => i.Payments)
            .Include(i => i.PurchaseOrder).ThenInclude(o => o.Supplier).OrderByDescending(i => i.Id).ToListAsync();
        return Ok(invoices.Select(i => new
        {
            i.Id, i.Number, i.PurchaseOrderId, supplierName = i.PurchaseOrder.Supplier.Name,
            i.InvoiceDate, i.DueDate, i.Status,
            total = i.Lines.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice)),
            paid = i.Payments.Sum(p => p.Amount),
            outstanding = i.Status == "Cancelled" ? 0 : i.Lines.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice)) - i.Payments.Sum(p => p.Amount),
            lines = i.Lines.Select(l => new { l.GoodsReceiptLineId, l.Quantity, l.UnitPrice }),
            payments = i.Payments.Select(p => new { p.Id, p.Amount, p.Date, p.Reference })
        }));
    }

    [HttpGet("billable-lines")]
    public async Task<IActionResult> Billable()
    {
        var lines = await db.GoodsReceiptLines.AsNoTracking().Where(l => l.GoodsReceipt.Status == "Posted")
            .Select(l => new
            {
                l.Id, l.GoodsReceiptId, l.GoodsReceipt.PurchaseOrderId,
                supplierName = l.GoodsReceipt.PurchaseOrder.Supplier.Name,
                materialName = l.PurchaseOrderLine.MaterialRequestLine.Material.Name,
                l.UnitCost, l.Quantity, l.GoodsReceipt.ReceivedDate
            }).ToListAsync();
        var billed = await db.SupplierInvoiceLines.Where(l => l.SupplierInvoice.Status == "Posted").GroupBy(l => l.GoodsReceiptLineId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(l => l.Quantity) }).ToDictionaryAsync(g => g.Id, g => g.Quantity);
        var returned = await db.InventoryOperations.Where(o => o.Kind == "SupplierReturn" && o.GoodsReceiptLineId != null && o.Status == "Posted")
            .GroupBy(o => o.GoodsReceiptLineId!.Value).Select(g => new { Id = g.Key, Quantity = g.Sum(o => o.Quantity) }).ToDictionaryAsync(g => g.Id, g => g.Quantity);
        return Ok(lines.Select(l => new { l.Id, l.GoodsReceiptId, l.PurchaseOrderId, l.supplierName, l.materialName, l.UnitCost, l.ReceivedDate,
            remainingQuantity = l.Quantity - billed.GetValueOrDefault(l.Id) - returned.GetValueOrDefault(l.Id) }).Where(l => l.remainingQuantity > 0));
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> Invoice(InvoiceDto dto)
    {
        if (!Guid.TryParse(dto.ClientRequestId, out var key) || key == Guid.Empty || string.IsNullOrWhiteSpace(dto.Number) ||
            dto.InvoiceDate == default || dto.DueDate < dto.InvoiceDate || dto.Lines.Count is < 1 or > 100 ||
            dto.Lines.Any(l => l is null) || dto.Lines.Select(l => l.ReceiptLineId).Distinct().Count() != dto.Lines.Count ||
            dto.Lines.Any(l => l.Quantity <= 0 || !OperationRules.Quantity(l.Quantity) || !OperationRules.Price(l.UnitPrice)))
            return BadRequest(new { message = "Check invoice number, dates, and unique valid receipt lines." });
        var orderInfo = await db.PurchaseOrders.Where(o => o.Id == dto.PurchaseOrderId).Select(o => new { o.SupplierId }).SingleOrDefaultAsync();
        if (orderInfo is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await db.Suppliers.FromSqlInterpolated($"SELECT * FROM `Suppliers` WHERE `Id` = {orderInfo.SupplierId} FOR UPDATE").ToListAsync();
        var order = (await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {dto.PurchaseOrderId} FOR UPDATE").ToListAsync()).Single();
        var existing = await db.SupplierInvoices.Include(i => i.Lines).SingleOrDefaultAsync(i => i.ClientRequestId == key.ToString("D"));
        if (existing is not null)
        {
            if (existing.CreatedByUserId != db.CurrentUserId || existing.PurchaseOrderId != dto.PurchaseOrderId || existing.Number != dto.Number.Trim() ||
                existing.InvoiceDate != dto.InvoiceDate || existing.DueDate != dto.DueDate || existing.Lines.Count != dto.Lines.Count ||
                existing.Lines.Any(l => !dto.Lines.Any(x => x.ReceiptLineId == l.GoodsReceiptLineId && x.Quantity == l.Quantity && x.UnitPrice == l.UnitPrice)))
                return Conflict(new { message = "Submission ID was used for a different invoice." });
            return Ok(new { existing.Id, message = "Invoice already recorded." });
        }
        if (order.Status is not ("Issued" or "PartiallyReceived" or "Received")) return Conflict(new { message = "The purchase order must be issued." });
        var ids = dto.Lines.Select(l => l.ReceiptLineId).ToList();
        var receipts = await db.GoodsReceiptLines.Include(l => l.GoodsReceipt)
            .Where(l => ids.Contains(l.Id) && l.GoodsReceipt.PurchaseOrderId == order.Id && l.GoodsReceipt.Status == "Posted").ToDictionaryAsync(l => l.Id);
        foreach (var line in dto.Lines)
        {
            if (!receipts.TryGetValue(line.ReceiptLineId, out var receipt) || line.UnitPrice != receipt.UnitCost || dto.InvoiceDate < receipt.GoodsReceipt.ReceivedDate)
                return BadRequest(new { message = "Invoice lines must match posted receipts, agreed order prices, and receipt dates." });
            var billed = await db.SupplierInvoiceLines.Where(l => l.GoodsReceiptLineId == receipt.Id && l.SupplierInvoice.Status == "Posted").SumAsync(l => (decimal?)l.Quantity) ?? 0;
            var returned = await db.InventoryOperations.Where(o => o.GoodsReceiptLineId == receipt.Id && o.Kind == "SupplierReturn" && o.Status == "Posted").SumAsync(o => (decimal?)o.Quantity) ?? 0;
            if (line.Quantity > receipt.Quantity - billed - returned) return Conflict(new { message = "Invoice quantity exceeds received, unreturned, uninvoiced materials." });
        }
        var invoice = new SupplierInvoice
        {
            ClientRequestId = key.ToString("D"), SupplierId = order.SupplierId, PurchaseOrderId = order.Id,
            Number = dto.Number.Trim(), InvoiceDate = dto.InvoiceDate, DueDate = dto.DueDate, CreatedByUserId = db.CurrentUserId,
            Lines = dto.Lines.Select(l => new SupplierInvoiceLine { GoodsReceiptLineId = l.ReceiptLineId, Quantity = l.Quantity, UnitPrice = l.UnitPrice }).ToList()
        };
        db.SupplierInvoices.Add(invoice);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { invoice.Id, message = "Invoice matched and recorded." });
    }

    [HttpPost("invoices/{id:int}/payments")]
    public async Task<IActionResult> Pay(int id, PaymentDto dto)
    {
        if (!Guid.TryParse(dto.ClientRequestId, out var key) || key == Guid.Empty || dto.Amount <= 0 || !OperationRules.Price(dto.Amount) ||
            dto.Date == default || string.IsNullOrWhiteSpace(dto.Reference)) return BadRequest(new { message = "Enter a positive payment, date, and reference." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var invoice = (await db.SupplierInvoices.FromSqlInterpolated($"SELECT * FROM `SupplierInvoices` WHERE `Id` = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (invoice is null) return NotFound();
        var existing = await db.SupplierPayments.SingleOrDefaultAsync(p => p.ClientRequestId == key.ToString("D"));
        if (existing is not null)
        {
            if (existing.SupplierInvoiceId != id || existing.Amount != dto.Amount || existing.Date != dto.Date || existing.Reference != dto.Reference.Trim() || existing.CreatedByUserId != db.CurrentUserId)
                return Conflict(new { message = "Submission ID was used for different payment data." });
            return Ok(new { message = "Payment already recorded." });
        }
        if (invoice.Status != "Posted" || dto.Date < invoice.InvoiceDate) return Conflict(new { message = "Invoice must be posted and payment date cannot precede it." });
        var lines = await db.SupplierInvoiceLines.Where(l => l.SupplierInvoiceId == id).ToListAsync();
        var paid = await db.SupplierPayments.Where(p => p.SupplierInvoiceId == id).SumAsync(p => (decimal?)p.Amount) ?? 0;
        if (dto.Amount > lines.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice)) - paid) return Conflict(new { message = "Payment exceeds the outstanding invoice balance." });
        db.SupplierPayments.Add(new SupplierPayment { SupplierInvoiceId = id, ClientRequestId = key.ToString("D"), Amount = dto.Amount, Date = dto.Date, Reference = dto.Reference.Trim(), CreatedByUserId = db.CurrentUserId });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Payment recorded." });
    }

    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        var orderId = await db.SupplierInvoices.Where(i => i.Id == id).Select(i => (int?)i.PurchaseOrderId).SingleOrDefaultAsync();
        if (orderId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {orderId.Value} FOR UPDATE").ToListAsync();
        var invoice = (await db.SupplierInvoices.FromSqlInterpolated($"SELECT * FROM `SupplierInvoices` WHERE `Id` = {id} FOR UPDATE").ToListAsync()).Single();
        if (await db.SupplierPayments.AnyAsync(p => p.SupplierInvoiceId == id)) return Conflict(new { message = "Paid invoices cannot be cancelled." });
        invoice.Status = "Cancelled";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Unpaid invoice cancelled." });
    }
}

public class InvoiceDto
{
    [Required, StringLength(36)] public string ClientRequestId { get; set; } = "";
    public int PurchaseOrderId { get; set; }
    [Required, StringLength(100)] public string Number { get; set; } = "";
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<InvoiceLineDto> Lines { get; set; } = new();
}
public class InvoiceLineDto
{
    public int ReceiptLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
public class PaymentDto
{
    [Required, StringLength(36)] public string ClientRequestId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    [Required, StringLength(150)] public string Reference { get; set; } = "";
}
