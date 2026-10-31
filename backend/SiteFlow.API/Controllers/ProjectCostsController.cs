using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/project-costs")]
public class ProjectCostsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProjectCostsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-store";

        // Keep the four reads on one consistent database snapshot.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);

        var projects = await _context.Projects.AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new { p.Id, p.Code, p.Name, p.Status, p.Budget })
            .ToListAsync(cancellationToken);

        var orders = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.PurchaseOrder.Status == "Draft" ||
                l.PurchaseOrder.Status == "Issued" ||
                l.PurchaseOrder.Status == "PartiallyReceived" ||
                l.PurchaseOrder.Status == "Received")
            .Select(l => new
            {
                l.PurchaseOrder.MaterialRequest.ProjectId,
                l.PurchaseOrder.Status, l.Quantity, l.UnitPrice
            })
            .ToListAsync(cancellationToken);

        var receipts = await _context.GoodsReceiptLines.AsNoTracking()
            .Where(l => l.GoodsReceipt.Status == "Posted")
            .Select(l => new
            {
                l.GoodsReceipt.PurchaseOrder.MaterialRequest.ProjectId,
                l.Quantity, l.UnitCost
            })
            .ToListAsync(cancellationToken);

        var issues = await _context.MaterialIssueLines.AsNoTracking()
            .Where(l => l.MaterialIssue.Status == "Posted")
            .Select(l => new { l.MaterialIssue.ProjectId, l.Quantity, l.UnitCost })
            .ToListAsync(cancellationToken);

        var returns = await _context.InventoryOperations.AsNoTracking()
            .Where(o => o.Kind == "SupplierReturn" && o.Status == "Posted")
            .Select(o => new { o.ProjectId, o.Quantity, o.UnitCost }).ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Match the per-line rounding used by transaction detail responses.
        static decimal Value(decimal quantity, decimal price) =>
            decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero);

        var draftValues = orders.Where(l => l.Status == "Draft")
            .GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Value(l.Quantity, l.UnitPrice)));
        var committedValues = orders.Where(l => l.Status != "Draft")
            .GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Value(l.Quantity, l.UnitPrice)));
        var returnedValues = returns.GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Value(l.Quantity, l.UnitCost)));
        var receivedValues = receipts.GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Value(l.Quantity, l.UnitCost)));
        var consumedValues = issues.GroupBy(l => l.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(l => Value(l.Quantity, l.UnitCost)));

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            projects = projects.Select(p => new
            {
                p.Id, p.Code, p.Name, p.Status, p.Budget,
                draftOrderValue = draftValues.GetValueOrDefault(p.Id),
                committedPurchaseValue = committedValues.GetValueOrDefault(p.Id),
                receivedMaterialValue = receivedValues.GetValueOrDefault(p.Id) - returnedValues.GetValueOrDefault(p.Id),
                consumedMaterialCost = consumedValues.GetValueOrDefault(p.Id),
                remainingMaterialBudget = p.Budget - consumedValues.GetValueOrDefault(p.Id),
                budgetAfterCommitments = p.Budget - committedValues.GetValueOrDefault(p.Id)
            })
        });
    }
}
