using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] int? projectId,
        [FromQuery] int? materialId,
        CancellationToken cancellationToken)
    {
        if (projectId is <= 0 || materialId is <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Project and material IDs must be positive."
            });
        }

        var query = _context.StockMovements
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(m =>
                m.ProjectId == projectId.Value);
        }

        if (materialId.HasValue)
        {
            query = query.Where(m =>
                m.MaterialId == materialId.Value);
        }

        var balances = await query
            .GroupBy(m => new
            {
                m.ProjectId,
                ProjectCode = m.Project.Code,
                ProjectName = m.Project.Name,
                m.MaterialId,
                MaterialCode = m.Material.Code,
                MaterialName = m.Material.Name,
                Unit = m.Material.Unit
            })
            .Select(group => new
            {
                group.Key.ProjectId,
                group.Key.ProjectCode,
                group.Key.ProjectName,
                group.Key.MaterialId,
                group.Key.MaterialCode,
                group.Key.MaterialName,
                group.Key.Unit,

                QuantityOnHand = group.Sum(m => m.Quantity)
            })
            .OrderBy(item => item.ProjectCode)
            .ThenBy(item => item.MaterialCode)
            .ToListAsync(cancellationToken);

        return Ok(balances);
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] int? projectId,
        [FromQuery] int? materialId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (projectId is <= 0 || materialId is <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Project and material IDs must be positive."
            });
        }

        if (page < 1 || page > 1000000 ||
            pageSize < 1 || pageSize > 200)
        {
            return BadRequest(new
            {
                message =
                    "Page must be between 1 and 1000000, and pageSize between 1 and 200."
            });
        }

        var query = _context.StockMovements
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(m =>
                m.ProjectId == projectId.Value);
        }

        if (materialId.HasValue)
        {
            query = query.Where(m =>
                m.MaterialId == materialId.Value);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await query
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.ProjectId,

                ProjectCode = m.Project.Code,
                ProjectName = m.Project.Name,

                m.MaterialId,

                MaterialCode = m.Material.Code,
                MaterialName = m.Material.Name,
                Unit = m.Material.Unit,

                m.MovementType,
                m.Quantity,
                m.UnitCost,
                m.MovementDate,

                m.GoodsReceiptLineId,

                GoodsReceiptId = m.GoodsReceiptLine == null
                    ? (int?)null
                    : m.GoodsReceiptLine.GoodsReceiptId,

                m.MaterialIssueLineId,

                MaterialIssueId = m.MaterialIssueLine == null
                    ? (int?)null
                    : m.MaterialIssueLine.MaterialIssueId,

                CreatedByName = m.CreatedByUser.FullName,
                m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            items
        });
    }

    [HttpPost("import-existing-receipts")]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> ImportExistingReceipts(
        CancellationToken cancellationToken)
    {
        var orderIds = await _context.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.Status == "Posted")
            .Select(r => r.PurchaseOrderId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        var addedMovements = 0;

        foreach (var orderId in orderIds)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            var lockedOrders = await _context.PurchaseOrders
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM `PurchaseOrders`
                    WHERE `Id` = {orderId}
                    FOR UPDATE
                    """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            if (lockedOrders.Count == 0)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                continue;
            }

            addedMovements +=
                await ReceiptStockPosting.PostMissingAsync(
                    _context,
                    orderId,
                    cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _context.ChangeTracker.Clear();
        }

        return Ok(new
        {
            message = "Existing posted receipts were imported.",
            addedMovements
        });
    }
}