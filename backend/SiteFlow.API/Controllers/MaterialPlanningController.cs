using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/material-planning")]
public class MaterialPlanningController(AppDbContext db) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(MaterialPlanningDto dto, CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-store";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dto.RequiredDate < today || dto.RequiredDate > today.AddYears(5))
            return BadRequest(new { message = "Required date must be today or within the next five years (UTC)." });
        if (dto.Lines is null || dto.Lines.Count is < 1 or > 100 ||
            dto.Lines.Any(l => l is null) ||
            dto.Lines.Select(l => l.MaterialId).Distinct().Count() != dto.Lines.Count)
            return BadRequest(new { message = "Add 1–100 unique material lines." });
        if (dto.Lines.Any(l => l.Quantity <= 0 || !OperationRules.Quantity(l.Quantity) ||
            l.SafetyStock < 0 || !OperationRules.Quantity(l.SafetyStock) ||
            l.Quantity + l.SafetyStock > 999999999.999m ||
            !OperationRules.Price(l.EstimatedUnitPrice) || l.LeadTimeDays is < 0 or > 365))
            return BadRequest(new { message = "Check quantity precision (3 decimals), price precision (2 decimals), and lead time (0–365 days)." });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        // Existing global filters enforce project assignments, including the downstream reads.
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == dto.ProjectId, ct);
        if (project is null) return NotFound(new { message = "Project not found or not assigned to you." });
        if (project.Status != "Active") return BadRequest(new { message = "Choose an active project." });
        var ids = dto.Lines.Select(l => l.MaterialId).ToArray();
        var materials = await db.Materials.AsNoTracking().Where(m => ids.Contains(m.Id) && m.IsActive).ToDictionaryAsync(m => m.Id, ct);
        if (materials.Count != ids.Length) return BadRequest(new { message = "Choose active materials." });
        var stock = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProjectId == project.Id && ids.Contains(m.MaterialId))
            .GroupBy(m => m.MaterialId).Select(g => new { Id = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(m => m.Id, m => m.Quantity, ct);
        var reserved = await db.MaterialReservations.AsNoTracking()
            .Where(r => r.ProjectId == project.Id && ids.Contains(r.MaterialId))
            .GroupBy(r => r.MaterialId).Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.Quantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToDictionaryAsync(r => r.Id, r => r.Quantity, ct);
        var orderLines = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.PurchaseOrder.MaterialRequest.ProjectId == project.Id &&
                ids.Contains(l.MaterialRequestLine.MaterialId) &&
                (l.PurchaseOrder.Status == "Issued" || l.PurchaseOrder.Status == "PartiallyReceived"))
            .Select(l => new { l.Id, l.Quantity, l.PurchaseOrder.ExpectedDeliveryDate, l.MaterialRequestLine.MaterialId })
            .ToListAsync(ct);
        var received = await db.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.GoodsReceipt.Status == "Posted" &&
                l.GoodsReceipt.PurchaseOrder.MaterialRequest.ProjectId == project.Id &&
                ids.Contains(l.PurchaseOrderLine.MaterialRequestLine.MaterialId))
            .GroupBy(l => l.PurchaseOrderLineId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(l => l.Id, l => l.Quantity, ct);
        var commitments = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.PurchaseOrder.MaterialRequest.ProjectId == project.Id &&
                (l.PurchaseOrder.Status == "Issued" || l.PurchaseOrder.Status == "PartiallyReceived" || l.PurchaseOrder.Status == "Received"))
            .Select(l => new { l.Quantity, l.UnitPrice }).ToListAsync(ct);
        await tx.CommitAsync(ct);

        var lines = dto.Lines.Select(input =>
        {
            var material = materials[input.MaterialId];
            var incoming = orderLines.Where(l => l.MaterialId == input.MaterialId)
                .Select(l => new PlanningDelivery(l.ExpectedDeliveryDate,
                    Math.Max(0, l.Quantity - received.GetValueOrDefault(l.Id))));
            var result = MaterialPlanningRules.Calculate(input.Quantity, input.SafetyStock,
                ReservationRules.Available(stock.GetValueOrDefault(input.MaterialId), reserved.GetValueOrDefault(input.MaterialId)), incoming, today, dto.RequiredDate,
                input.LeadTimeDays, input.EstimatedUnitPrice);
            return new
            {
                materialId = material.Id, material.Code, material.Name, material.Unit,
                demand = input.Quantity, input.SafetyStock, input.LeadTimeDays, input.EstimatedUnitPrice,
                physicalStock = stock.GetValueOrDefault(input.MaterialId), reservedStock = reserved.GetValueOrDefault(input.MaterialId),
                result.Stock, result.Incoming, result.OverdueIncoming, result.LateIncoming,
                result.Shortage, result.EstimatedCost, result.OrderByDate, result.OrderIsLate,
                status = result.Shortage == 0 ? "Covered" : result.OrderIsLate ? "Urgent" : "OrderNeeded"
            };
        }).ToList();
        var committed = commitments.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice));
        var estimatedCost = lines.Sum(l => l.EstimatedCost);
        return Ok(new
        {
            projectId = project.Id, projectName = project.Name, dto.RequiredDate,
            generatedAtUtc = DateTime.UtcNow, asOfDate = today,
            project.Budget, committedPurchaseValue = committed,
            budgetAfterCommitments = project.Budget - committed,
            estimatedCost, budgetAfterSuggestedPurchase = project.Budget - committed - estimatedCost,
            missingPriceCount = lines.Count(l => l.Shortage > 0 && l.EstimatedUnitPrice == 0),
            lines,
            assumptions = new[] {
                "Demand is additional upcoming work; no usage history is required.",
                "Available stock is the recorded ledger balance minus remaining reservations, including posted corrections and returns.",
                "Incoming supply includes only unreceived quantities on issued or partially received orders expected from today through the required date.",
                "Overdue promises, later deliveries, draft orders, and cancelled orders do not cover demand.",
                "Supplier returns reduce stock but do not automatically reopen completed purchase orders.",
                "Lead times, safety quantities, and prices are user assumptions. Estimates exclude tax and delivery charges.",
                "This preview does not reserve stock or account for other unsaved plans or material requests. Review existing commitments before ordering.",
                "Budget comparison uses issued purchase-order commitments; draft orders are excluded."
            }
        });
    }
}

public class MaterialPlanningDto
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    public DateOnly RequiredDate { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<MaterialPlanningLineDto> Lines { get; set; } = new();
}
public class MaterialPlanningLineDto
{
    [Range(1, int.MaxValue)] public int MaterialId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999.999")] public decimal Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999.999")] public decimal SafetyStock { get; set; }
    [Range(0, 365)] public int LeadTimeDays { get; set; }
    [Range(typeof(decimal), "0", "999999999.99")] public decimal EstimatedUnitPrice { get; set; }
}
