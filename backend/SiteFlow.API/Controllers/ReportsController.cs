using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/reports")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("{kind}/{format}")]
    public async Task<IActionResult> Download(string kind, string format, [FromQuery] int? projectId, [FromQuery] int? orderId)
    {
        if (format is not ("pdf" or "xlsx") || projectId is <= 0 || orderId is <= 0) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var rows = new List<string[]>();
        static string N(decimal n) => n.ToString("0.00", CultureInfo.InvariantCulture);
        if (kind == "orders")
        {
            rows.Add(new[] { "Purchase order", "Project", "Supplier", "Order date", "Delivery date", "Status", "Material", "Unit", "Quantity", "Unit price LKR", "Line total LKR" });
            var lines = await db.PurchaseOrderLines.AsNoTracking().Where(l => (!projectId.HasValue || l.PurchaseOrder.MaterialRequest.ProjectId == projectId) && (!orderId.HasValue || l.PurchaseOrderId == orderId))
                .Select(l => new { l.PurchaseOrderId, project = l.PurchaseOrder.MaterialRequest.Project.Name, supplier = l.PurchaseOrder.Supplier.Name,
                    l.PurchaseOrder.OrderDate, l.PurchaseOrder.ExpectedDeliveryDate, l.PurchaseOrder.Status, material = l.MaterialRequestLine.Material.Name,
                    l.MaterialRequestLine.Material.Unit, l.Quantity, l.UnitPrice }).ToListAsync();
            rows.AddRange(lines.Select(l => new[] { $"PO-{l.PurchaseOrderId:D6}", l.project, l.supplier, l.OrderDate.ToString("yyyy-MM-dd"), l.ExpectedDeliveryDate.ToString("yyyy-MM-dd"), l.Status, l.material, l.Unit, l.Quantity.ToString(CultureInfo.InvariantCulture), N(l.UnitPrice), N(OperationRules.Money(l.Quantity, l.UnitPrice)) }));
        }
        else if (kind == "stock")
        {
            rows.Add(new[] { "Project", "Store / location", "Material", "Date", "Type", "Quantity", "Unit cost LKR", "Receipt line", "Issue line", "Operation", "Reverses movement" });
            var movements = await db.StockMovements.AsNoTracking().Where(m => !projectId.HasValue || m.ProjectId == projectId)
                .OrderBy(m => m.MovementDate).ThenBy(m => m.Id)
                .Select(m => new { project = m.Project.Name, location = m.StorageLocation.Store.Code + " / " + m.StorageLocation.Code, material = m.Material.Name, m.MovementDate, m.MovementType, m.Quantity, m.UnitCost, m.GoodsReceiptLineId, m.MaterialIssueLineId, m.InventoryOperationId, m.ReversesMovementId }).ToListAsync();
            rows.AddRange(movements.Select(m => new[] { m.project, m.location, m.material, m.MovementDate.ToString("yyyy-MM-dd"), m.MovementType, m.Quantity.ToString(CultureInfo.InvariantCulture), N(m.UnitCost), m.GoodsReceiptLineId?.ToString() ?? "", m.MaterialIssueLineId?.ToString() ?? "", m.InventoryOperationId?.ToString() ?? "", m.ReversesMovementId?.ToString() ?? "" }));
        }
        else if (kind == "costs")
        {
            rows.Add(new[] { "Project", "Budget LKR", "Confirmed purchases LKR", "Net receipts LKR", "Consumed LKR", "Budget less consumed LKR" });
            var projects = await db.Projects.AsNoTracking().Where(p => !projectId.HasValue || p.Id == projectId).OrderBy(p => p.Code).ToListAsync();
            foreach (var p in projects)
            {
                var orders = await db.PurchaseOrderLines.Where(l => l.PurchaseOrder.MaterialRequest.ProjectId == p.Id &&
                    (l.PurchaseOrder.Status == "Issued" || l.PurchaseOrder.Status == "PartiallyReceived" || l.PurchaseOrder.Status == "Received"))
                    .Select(l => new { l.Quantity, l.UnitPrice }).ToListAsync();
                var receipts = await db.GoodsReceiptLines.Where(l => l.GoodsReceipt.PurchaseOrder.MaterialRequest.ProjectId == p.Id && l.GoodsReceipt.Status == "Posted")
                    .Select(l => new { l.Quantity, l.UnitCost }).ToListAsync();
                var returns = await db.InventoryOperations.Where(o => o.ProjectId == p.Id && o.Kind == "SupplierReturn" && o.Status == "Posted").Select(o => new { o.Quantity, o.UnitCost }).ToListAsync();
                var issues = await db.MaterialIssueLines.Where(l => l.MaterialIssue.ProjectId == p.Id && l.MaterialIssue.Status == "Posted").Select(l => new { l.Quantity, l.UnitCost }).ToListAsync();
                var consumed = issues.Sum(l => OperationRules.Money(l.Quantity, l.UnitCost));
                rows.Add(new[] { $"{p.Code} - {p.Name}", N(p.Budget), N(orders.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice))),
                    N(receipts.Sum(l => OperationRules.Money(l.Quantity, l.UnitCost)) - returns.Sum(l => OperationRules.Money(l.Quantity, l.UnitCost))), N(consumed), N(p.Budget - consumed) });
            }
        }
        else return BadRequest(new { message = "Choose orders, stock, or costs." });
        Response.Headers["Cache-Control"] = "no-store";
        var bytes = format == "pdf" ? ReportFiles.Pdf(kind, rows) : ReportFiles.Excel(rows);
        return File(bytes, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SiteFlow-{kind}-{DateTime.UtcNow:yyyyMMdd}.{format}");
    }
}
