using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize(Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper), Route("api/inventory-operations")]
public class InventoryOperationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.InventoryOperations.AsNoTracking().OrderByDescending(o => o.Id).Take(200)
        .Select(o => new { o.Id, o.Kind, o.Status, o.ProjectId, projectName = o.Project.Name, o.DestinationProjectId,
            o.MaterialId, materialName = o.Material.Name, o.Quantity, o.UnitCost, o.Date, o.Reason, o.CreatedAtUtc }).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Post(InventoryOperationDto dto)
    {
        if (!Guid.TryParse(dto.ClientRequestId, out var key) || key == Guid.Empty ||
            dto.Date == default || string.IsNullOrWhiteSpace(dto.Reason) ||
            !OperationRules.Quantity(dto.Quantity) || !OperationRules.Price(dto.UnitCost) ||
            !new[] { "Adjustment", "Count", "Transfer", "SupplierReturn" }.Contains(dto.Kind) ||
            (dto.Kind == "Count" ? dto.Quantity < 0 : dto.Quantity == 0) ||
            (dto.Kind is "Transfer" or "SupplierReturn" && dto.Quantity <= 0) ||
            (dto.Kind == "Transfer" && (dto.DestinationProjectId is null || dto.DestinationProjectId == dto.ProjectId)) ||
            (dto.Kind != "Transfer" && dto.DestinationProjectId is not null) ||
            (dto.Kind == "SupplierReturn" ? dto.GoodsReceiptLineId is null : dto.GoodsReceiptLineId is not null))
            return BadRequest(new { message = "Check the operation, date, quantities, unit cost, destination, receipt line, and reason." });

        dto.ClientRequestId = key.ToString("D");
        dto.Reason = dto.Reason.Trim();
        var requestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(dto))));
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        GoodsReceiptLine? receiptLine = null;
        if (dto.Kind == "SupplierReturn")
        {
            receiptLine = await db.GoodsReceiptLines.Include(l => l.GoodsReceipt).ThenInclude(r => r.PurchaseOrder).ThenInclude(o => o.MaterialRequest)
                .Include(l => l.PurchaseOrderLine).ThenInclude(l => l.MaterialRequestLine)
                .SingleOrDefaultAsync(l => l.Id == dto.GoodsReceiptLineId);
            if (receiptLine is null || receiptLine.GoodsReceipt.Status != "Posted" ||
                receiptLine.GoodsReceipt.PurchaseOrder.MaterialRequest.ProjectId != dto.ProjectId ||
                receiptLine.PurchaseOrderLine.MaterialRequestLine.MaterialId != dto.MaterialId)
                return BadRequest(new { message = "Select a posted receipt line belonging to this project and material." });
            await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {receiptLine.GoodsReceipt.PurchaseOrderId} FOR UPDATE").ToListAsync();
        }
        foreach (var projectId in new[] { dto.ProjectId, dto.DestinationProjectId ?? dto.ProjectId }.Distinct().OrderBy(id => id))
            if (!await OperationRules.LockProject(db, projectId)) return BadRequest(new { message = "Select active projects assigned to you." });

        var existing = await db.InventoryOperations.SingleOrDefaultAsync(o => o.ClientRequestId == key.ToString("D"));
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash) return Conflict(new { message = "Submission ID was already used for different data." });
            if (existing.CreatedByUserId != db.CurrentUserId || existing.ProjectId != dto.ProjectId || existing.MaterialId != dto.MaterialId ||
                existing.Kind != dto.Kind || existing.Quantity != dto.Quantity || existing.Date != dto.Date || existing.Reason != dto.Reason.Trim() ||
                existing.DestinationProjectId != dto.DestinationProjectId || existing.GoodsReceiptLineId != dto.GoodsReceiptLineId)
                return Conflict(new { message = "Submission ID was already used for different data." });
            return Ok(new { existing.Id, message = "Operation already posted." });
        }
        if (!await db.Materials.AnyAsync(m => m.Id == dto.MaterialId && m.IsActive)) return BadRequest(new { message = "Select an active material." });
        // Do not count or move stock whose historical receipts have not been imported.
        if (await db.GoodsReceiptLines.AnyAsync(l => l.GoodsReceipt.Status == "Posted" &&
            l.GoodsReceipt.PurchaseOrder.MaterialRequest.ProjectId == dto.ProjectId && l.PurchaseOrderLine.MaterialRequestLine.MaterialId == dto.MaterialId &&
            !db.StockMovements.Any(m => m.GoodsReceiptLineId == l.Id)))
            return Conflict(new { message = "Import existing receipts before changing this stock." });
        var stock = await OperationRules.Stock(db, dto.ProjectId, dto.MaterialId);
        if (dto.Date < stock.Latest) return BadRequest(new { message = "Date cannot precede the latest stock movement." });
        var change = dto.Kind == "Count" ? dto.Quantity - stock.Quantity : dto.Kind is "Transfer" or "SupplierReturn" ? -dto.Quantity : dto.Quantity;
        if (!ReservationRules.CanReduceTo(stock.Quantity + change, await ReservationRules.Held(db, dto.ProjectId, dto.MaterialId))) return Conflict(new { message = "Insufficient unreserved stock. Release reservations before reducing stock below the reserved quantity." });
        var cost = change < 0 || (dto.Kind == "Count" && stock.Quantity > 0) ? stock.Cost : dto.UnitCost;
        if (receiptLine is not null)
        {
            // Re-read after the purchase order lock in case a reversal won the race.
            if (!await db.GoodsReceipts.AnyAsync(r => r.Id == receiptLine.GoodsReceiptId && r.Status == "Posted"))
                return Conflict(new { message = "Receipt has been reversed." });
            if (await db.SupplierInvoiceLines.AnyAsync(l => l.GoodsReceiptLineId == receiptLine.Id && l.SupplierInvoice.Status == "Posted"))
                return Conflict(new { message = "Cancel the unpaid invoice before returning these materials." });
            var returned = await db.InventoryOperations.Where(o => o.GoodsReceiptLineId == receiptLine.Id && o.Kind == "SupplierReturn" && o.Status == "Posted").SumAsync(o => (decimal?)o.Quantity) ?? 0;
            if (dto.Quantity > receiptLine.Quantity - returned) return Conflict(new { message = "Return quantity exceeds the unreturned receipt quantity." });
            cost = receiptLine.UnitCost;
            var availableValue = await db.StockMovements.Where(m => m.ProjectId == dto.ProjectId && m.MaterialId == dto.MaterialId).SumAsync(m => (decimal?)(m.Quantity * m.UnitCost)) ?? 0;
            if (availableValue < dto.Quantity * cost) return Conflict(new { message = "The return exceeds the remaining stock value. Review dependent material usage first." });
        }
        var operation = new InventoryOperation
        {
            RequestHash = requestHash,
            ClientRequestId = key.ToString("D"), ProjectId = dto.ProjectId, DestinationProjectId = dto.DestinationProjectId,
            MaterialId = dto.MaterialId, Kind = dto.Kind, Quantity = dto.Quantity, UnitCost = cost,
            Date = dto.Date, Reason = dto.Reason.Trim(), CreatedByUserId = db.CurrentUserId, GoodsReceiptLineId = dto.GoodsReceiptLineId
        };
        db.InventoryOperations.Add(operation);
        if (change != 0)
        {
            var movement = OperationRules.Movement(db, dto.ProjectId, dto.MaterialId, dto.Kind == "Transfer" ? "TransferOut" : dto.Kind, change, cost, dto.Date);
            movement.InventoryOperation = operation;
            db.StockMovements.Add(movement);
        }
        if (dto.Kind == "Transfer")
        {
            var destination = await OperationRules.Stock(db, dto.DestinationProjectId!.Value, dto.MaterialId);
            if (dto.Date < destination.Latest) return BadRequest(new { message = "Date precedes the destination's latest stock movement." });
            var incoming = OperationRules.Movement(db, dto.DestinationProjectId.Value, dto.MaterialId, "TransferIn", dto.Quantity, cost, dto.Date);
            incoming.InventoryOperation = operation;
            db.StockMovements.Add(incoming);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { operation.Id, message = "Inventory operation posted." });
    }

    [HttpPost("{id:int}/reverse")]
    public async Task<IActionResult> Reverse(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        var info = await db.InventoryOperations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id);
        if (info is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        if (info.GoodsReceiptLineId is not null)
        {
            var orderId = await db.GoodsReceiptLines.Where(l => l.Id == info.GoodsReceiptLineId).Select(l => l.GoodsReceipt.PurchaseOrderId).SingleAsync();
            await db.PurchaseOrders.FromSqlInterpolated($"SELECT * FROM `PurchaseOrders` WHERE `Id` = {orderId} FOR UPDATE").ToListAsync();
        }
        foreach (var project in new[] { info.ProjectId, info.DestinationProjectId ?? info.ProjectId }.Distinct().OrderBy(p => p))
            if (!await OperationRules.LockProject(db, project)) return BadRequest(new { message = "Both projects must be active and assigned to you." });
        var operation = await db.InventoryOperations.SingleAsync(o => o.Id == id);
        if (operation.Status != "Posted") return Conflict(new { message = "Operation already reversed." });
        var movements = await db.StockMovements.Where(m => m.InventoryOperationId == id).ToListAsync();
        foreach (var movement in movements)
        {
            var stock = await OperationRules.Stock(db, movement.ProjectId, movement.MaterialId);
            if (!ReservationRules.CanReduceTo(stock.Quantity - movement.Quantity, await ReservationRules.Held(db, movement.ProjectId, movement.MaterialId))) return Conflict(new { message = "Reversal would consume reserved stock or create negative stock. Release holds or reverse dependent activity first." });
            if (movement.Quantity > 0 && await db.StockMovements.AnyAsync(m => m.ProjectId == movement.ProjectId && m.MaterialId == movement.MaterialId && m.Id > movement.Id &&
                m.ReversesMovementId == null && !db.StockMovements.Any(r => r.ReversesMovementId == m.Id)))
                return Conflict(new { message = "Reverse later stock activity first to preserve valuation." });
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var reversal = OperationRules.Movement(db, movement.ProjectId, movement.MaterialId, "Reversal", -movement.Quantity, movement.UnitCost, date > stock.Latest ? date : stock.Latest);
            reversal.ReversesMovementId = movement.Id;
            db.StockMovements.Add(reversal);
        }
        operation.Status = "Reversed";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Inventory operation reversed with compensating movements." });
    }
}

public class InventoryOperationDto : ReasonDto
{
    [Required, StringLength(36)] public string ClientRequestId { get; set; } = "";
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    public int? DestinationProjectId { get; set; }
    [Range(1, int.MaxValue)] public int MaterialId { get; set; }
    [Required] public string Kind { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateOnly Date { get; set; }
    public int? GoodsReceiptLineId { get; set; }
}
