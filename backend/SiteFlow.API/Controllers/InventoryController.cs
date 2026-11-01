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
        [FromQuery] int? storageLocationId,
        CancellationToken cancellationToken)
    {
        if (projectId is <= 0 || materialId is <= 0 || storageLocationId is <= 0)
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

        await using var snapshot = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        query = query.Where(m => !storageLocationId.HasValue || m.StorageLocationId == storageLocationId);
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

        var held = await _context.MaterialReservations.AsNoTracking()
            .Where(r => (!projectId.HasValue || r.ProjectId == projectId) && (!materialId.HasValue || r.MaterialId == materialId) && (!storageLocationId.HasValue || r.StorageLocationId == storageLocationId))
            .GroupBy(r => new { r.ProjectId, r.MaterialId })
            .Select(g => new { g.Key.ProjectId, g.Key.MaterialId, Quantity = g.Sum(r => r.Quantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToListAsync(cancellationToken);
        var reserved = held.ToDictionary(r => (r.ProjectId, r.MaterialId), r => r.Quantity);
        await snapshot.CommitAsync(cancellationToken);
        return Ok(balances.Select(b => new { b.ProjectId, b.ProjectCode, b.ProjectName, b.MaterialId,
            b.MaterialCode, b.MaterialName, b.Unit, b.QuantityOnHand,
            reservedQuantity = reserved.GetValueOrDefault((b.ProjectId, b.MaterialId)),
            availableQuantity = ReservationRules.Available(b.QuantityOnHand, reserved.GetValueOrDefault((b.ProjectId, b.MaterialId))) }));
    }

    [HttpGet("location-balances")]
    public async Task<IActionResult> LocationBalances([FromQuery] int? projectId, [FromQuery] int? storageLocationId,
        [FromQuery] int? materialId, CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-store";
        if (projectId is <= 0 || storageLocationId is <= 0 || materialId is <= 0) return BadRequest();
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var stock = await _context.StockMovements.AsNoTracking()
            .Where(m => (!projectId.HasValue || m.ProjectId == projectId) &&
                (!storageLocationId.HasValue || m.StorageLocationId == storageLocationId) && (!materialId.HasValue || m.MaterialId == materialId))
            .GroupBy(m => new { m.ProjectId, projectName = m.Project.Name, projectCode = m.Project.Code,
                m.StorageLocationId, storeName = m.StorageLocation.Store.Name, storeCode = m.StorageLocation.Store.Code,
                locationName = m.StorageLocation.Name, locationCode = m.StorageLocation.Code, m.StorageLocation.Rack, m.StorageLocation.Bin,
                m.MaterialId, materialName = m.Material.Name, materialCode = m.Material.Code, m.Material.Unit })
            .Select(g => new { g.Key, Quantity = g.Sum(m => m.Quantity) })
            .OrderBy(g => g.Key.projectCode).ThenBy(g => g.Key.storeCode).ThenBy(g => g.Key.locationCode).ThenBy(g => g.Key.materialCode)
            .ToListAsync(ct);
        var held = await _context.MaterialReservations.AsNoTracking()
            .Where(r => (!projectId.HasValue || r.ProjectId == projectId) && (!storageLocationId.HasValue || r.StorageLocationId == storageLocationId) &&
                (!materialId.HasValue || r.MaterialId == materialId))
            .GroupBy(r => new { r.StorageLocationId, r.MaterialId })
            .Select(g => new { g.Key.StorageLocationId, g.Key.MaterialId, Quantity = g.Sum(r => r.Quantity - r.ConsumedQuantity - r.ReleasedQuantity) })
            .ToListAsync(ct);
        var reservations = held.ToDictionary(r => (r.StorageLocationId, r.MaterialId), r => r.Quantity);
        await tx.CommitAsync(ct);
        return Ok(stock.Select(s => new { s.Key.ProjectId, s.Key.projectName, s.Key.projectCode, s.Key.StorageLocationId,
            s.Key.storeName, s.Key.storeCode, s.Key.locationName, s.Key.locationCode, s.Key.Rack, s.Key.Bin,
            s.Key.MaterialId, s.Key.materialName, s.Key.materialCode, s.Key.Unit,
            quantityOnHand = s.Quantity, reservedQuantity = reservations.GetValueOrDefault((s.Key.StorageLocationId, s.Key.MaterialId)),
            availableQuantity = ReservationRules.Available(s.Quantity, reservations.GetValueOrDefault((s.Key.StorageLocationId, s.Key.MaterialId))) }));
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] int? projectId,
        [FromQuery] int? materialId,
        [FromQuery] int? storageLocationId = null,
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

        query = query.Where(m => !storageLocationId.HasValue || m.StorageLocationId == storageLocationId);
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
                m.StorageLocationId, storageLocationName = m.StorageLocation.Store.Code + " / " + m.StorageLocation.Code,
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
                m.InventoryOperationId,
                m.ReversesMovementId,

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
