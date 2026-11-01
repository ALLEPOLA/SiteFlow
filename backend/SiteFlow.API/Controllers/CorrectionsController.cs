using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/corrections")]
public class CorrectionsController(AppDbContext db) : ControllerBase
{
    [HttpPut("requests/{id:int}"), Authorize(Roles = AppRoles.Administrator + "," + AppRoles.SiteEngineer)]
    public async Task<IActionResult> EditRequest(int id, EditRequestDto dto)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var request = (await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM `MaterialRequests` WHERE `Id` = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (request is null) return NotFound();
        if (request.CreatedByUserId != db.CurrentUserId || request.Status != "Draft")
            return Conflict(new { message = "Only the creator can edit a draft request." });
        if (dto.RequiredDate == default || string.IsNullOrWhiteSpace(dto.ReasonText) || string.IsNullOrWhiteSpace(dto.Reason) ||
            dto.Lines.Count is < 1 or > 100 || dto.Lines.Any(l => l is null) || dto.Lines.Select(l => l.MaterialId).Distinct().Count() != dto.Lines.Count ||
            dto.Lines.Any(l => l.Quantity <= 0 || !OperationRules.Quantity(l.Quantity) || !OperationRules.Price(l.Price)) ||
            !await db.Projects.AnyAsync(p => p.Id == request.ProjectId && p.Status == "Active"))
            return BadRequest(new { message = "Enter a required date, reason, active project, and unique valid material lines." });
        var ids = dto.Lines.Select(l => l.MaterialId).ToList();
        if (await db.Materials.CountAsync(m => ids.Contains(m.Id) && m.IsActive) != ids.Count) return BadRequest(new { message = "Select active materials." });
        await db.Entry(request).Collection(r => r.Lines).LoadAsync();
        db.MaterialRequestLines.RemoveRange(request.Lines);
        await db.SaveChangesAsync();
        request.RequiredDate = dto.RequiredDate;
        request.Reason = dto.ReasonText.Trim();
        request.Lines = dto.Lines.Select(l => new MaterialRequestLine { MaterialId = l.MaterialId, Quantity = l.Quantity, EstimatedUnitPrice = l.Price }).ToList();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Draft request updated." });
    }

    [HttpPut("orders/{id:int}"), Authorize(Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<IActionResult> EditOrder(int id, EditOrderDto dto)
    {
        var requestId = await db.PurchaseOrders.Where(o => o.Id == id).Select(o => (int?)o.MaterialRequestId).SingleOrDefaultAsync();
        if (requestId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM `MaterialRequests` WHERE `Id` = {requestId.Value} FOR UPDATE").ToListAsync();
        var order = (await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {id} FOR UPDATE").ToListAsync()).Single();
        if (order.Status != "Draft") return Conflict(new { message = "Only draft purchase orders can be edited." });
        if (dto.OrderDate == default || dto.DeliveryDate < dto.OrderDate || string.IsNullOrWhiteSpace(dto.Reason) ||
            !await db.Suppliers.AnyAsync(s => s.Id == dto.SupplierId && s.IsActive) ||
            !await db.MaterialRequests.AnyAsync(r => r.Id == requestId && r.Status == "Approved" && r.Project.Status == "Active") ||
            dto.Lines.Count is < 1 or > 100 || dto.Lines.Any(l => l is null) || dto.Lines.Select(l => l.MaterialId).Distinct().Count() != dto.Lines.Count ||
            dto.Lines.Any(l => l.Quantity <= 0 || !OperationRules.Quantity(l.Quantity) || !OperationRules.Price(l.Price)))
            return BadRequest(new { message = "Check dates, supplier, reason, and unique valid order lines." });
        var approved = await db.MaterialRequestLines.Include(l => l.Material).Where(l => l.MaterialRequestId == requestId).ToDictionaryAsync(l => l.Id);
        var other = await db.PurchaseOrderLines.Where(l => l.PurchaseOrderId != id && l.PurchaseOrder.MaterialRequestId == requestId && l.PurchaseOrder.Status != "Cancelled")
            .GroupBy(l => l.MaterialRequestLineId).Select(g => new { Id = g.Key, Quantity = g.Sum(l => l.Quantity) }).ToDictionaryAsync(l => l.Id, l => l.Quantity);
        foreach (var line in dto.Lines)
            if (!approved.TryGetValue(line.MaterialId, out var source) || !source.Material.IsActive || line.Quantity > source.Quantity - other.GetValueOrDefault(source.Id))
                return Conflict(new { message = "An order line exceeds the remaining approved quantity or is not active." });
        await db.Entry(order).Collection(o => o.Lines).LoadAsync();
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        await db.SaveChangesAsync();
        order.SupplierId = dto.SupplierId;
        order.OrderDate = dto.OrderDate;
        order.ExpectedDeliveryDate = dto.DeliveryDate;
        order.Notes = dto.Notes?.Trim() ?? "";
        order.Lines = dto.Lines.Select(l => new PurchaseOrderLine { MaterialRequestLineId = l.MaterialId, Quantity = l.Quantity, UnitPrice = l.Price }).ToList();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Draft purchase order updated." });
    }

    [HttpPost("orders/{id:int}/cancel"), Authorize(Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<IActionResult> CancelOrder(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync();
        var order = (await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (order is null) return NotFound();
        if (order.Status == "Cancelled") return Ok(new { message = "Order already cancelled." });
        if (order.Status is not ("Draft" or "Issued") || await db.GoodsReceipts.AnyAsync(r => r.PurchaseOrderId == id && r.Status == "Posted") ||
            await db.SupplierInvoices.AnyAsync(i => i.PurchaseOrderId == id && i.Status == "Posted"))
            return Conflict(new { message = "Only draft or issued orders without posted receipts or invoices can be cancelled." });
        order.Status = "Cancelled";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Order cancelled. Reserved request quantities are available again." });
    }

    [HttpPost("receipts/{id:int}/reverse"), Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper)]
    public async Task<IActionResult> ReverseReceipt(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        var source = await db.GoodsReceipts.Where(r => r.Id == id).Select(r => new { r.PurchaseOrderId, r.PurchaseOrder.MaterialRequest.ProjectId }).SingleOrDefaultAsync();
        if (source is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var order = (await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {source.PurchaseOrderId} FOR UPDATE").ToListAsync()).Single();
        if (!await OperationRules.LockProject(db, source.ProjectId)) return BadRequest(new { message = "Project must be active." });
        var receipt = await db.GoodsReceipts.Include(r => r.Lines).SingleAsync(r => r.Id == id);
        if (receipt.Status != "Posted") return Conflict(new { message = "Receipt is already reversed." });
        var ids = receipt.Lines.Select(l => l.Id).ToList();
        if (await db.SupplierInvoiceLines.AnyAsync(l => ids.Contains(l.GoodsReceiptLineId) && l.SupplierInvoice.Status == "Posted") ||
            await db.InventoryOperations.AnyAsync(o => o.GoodsReceiptLineId != null && ids.Contains(o.GoodsReceiptLineId.Value) && o.Status == "Posted"))
            return Conflict(new { message = "This receipt has invoices or supplier returns. Resolve those records before reversing it." });
        var movements = await db.StockMovements.Where(m => m.GoodsReceiptLineId != null && ids.Contains(m.GoodsReceiptLineId.Value)).ToListAsync();
        if (movements.Count != ids.Count) return Conflict(new { message = "Import missing receipt stock before reversal." });
        foreach (var m in movements)
        {
            var stock = await OperationRules.Stock(db, m.ProjectId, m.MaterialId);
            if (!ReservationRules.CanReduceTo(stock.Quantity - m.Quantity, await ReservationRules.Held(db, m.ProjectId, m.MaterialId))) return Conflict(new { message = "Receipt reversal would remove reserved stock or create negative stock. Release holds or reverse dependent issues first." });
            // Preserve valuation history: remove a receipt only before later movements use its cost.
            if (await db.StockMovements.AnyAsync(x => x.ProjectId == m.ProjectId && x.MaterialId == m.MaterialId && x.Id > m.Id &&
                x.ReversesMovementId == null && !db.StockMovements.Any(r => r.ReversesMovementId == x.Id)))
                return Conflict(new { message = "Reverse later stock activity for this material first." });
            var reversal = OperationRules.Movement(db, m.ProjectId, m.MaterialId, "Reversal", -m.Quantity, m.UnitCost, DateOnly.FromDateTime(DateTime.UtcNow));
            reversal.MovementDate = stock.Latest > reversal.MovementDate ? stock.Latest : reversal.MovementDate;
            reversal.ReversesMovementId = m.Id;
            db.StockMovements.Add(reversal);
        }
        receipt.Status = "Reversed";
        var anyOther = await db.GoodsReceipts.AnyAsync(r => r.PurchaseOrderId == order.Id && r.Id != id && r.Status == "Posted");
        order.Status = anyOther ? "PartiallyReceived" : "Issued";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Receipt reversed with compensating stock movements." });
    }

    [HttpPost("issues/{id:int}/reverse"), Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper)]
    public async Task<IActionResult> ReverseIssue(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        var projectId = await db.MaterialIssues.Where(i => i.Id == id).Select(i => (int?)i.ProjectId).SingleOrDefaultAsync();
        if (projectId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        if (!await OperationRules.LockProject(db, projectId.Value)) return BadRequest(new { message = "Project must be active." });
        var issue = await db.MaterialIssues.Include(i => i.Lines).SingleAsync(i => i.Id == id);
        if (issue.Status != "Posted") return Conflict(new { message = "Issue is already reversed." });
        var ids = issue.Lines.Select(l => l.Id).ToList();
        var movements = await db.StockMovements.Where(m => m.MaterialIssueLineId != null && ids.Contains(m.MaterialIssueLineId.Value)).ToListAsync();
        if (movements.Count != ids.Count) return Conflict(new { message = "Issue stock history is incomplete." });
        foreach (var m in movements)
        {
            var stock = await OperationRules.Stock(db, m.ProjectId, m.MaterialId);
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var reversal = OperationRules.Movement(db, m.ProjectId, m.MaterialId, "Reversal", -m.Quantity, m.UnitCost, date > stock.Latest ? date : stock.Latest);
            reversal.ReversesMovementId = m.Id;
            db.StockMovements.Add(reversal);
        }
        foreach (var line in issue.Lines.Where(l => l.ReservationId.HasValue))
        {
            var hold = await db.MaterialReservations.SingleAsync(r => r.Id == line.ReservationId);
            if (hold.ConsumedQuantity < line.Quantity) return Conflict(new { message = "Reservation consumption history is inconsistent." });
            hold.ConsumedQuantity -= line.Quantity;
        }
        issue.Status = "Reversed";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Material issue reversed; stock restored." });
    }
}

public class ReasonDto
{
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
public class EditCostLineDto
{
    [Range(1, int.MaxValue)] public int MaterialId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}
public class EditRequestDto : ReasonDto
{
    public DateOnly RequiredDate { get; set; }
    [Required, StringLength(1000)] public string ReasonText { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)] public List<EditCostLineDto> Lines { get; set; } = new();
}
public class EditOrderDto : ReasonDto
{
    public int SupplierId { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly DeliveryDate { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<EditCostLineDto> Lines { get; set; } = new();
}
