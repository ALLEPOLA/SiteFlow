using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.GoodsReceipts;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/goods-receipts")]
public class GoodsReceiptsController : ControllerBase
{
    private readonly AppDbContext _context;

    public GoodsReceiptsController(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<GoodsReceipt> ReceiptQuery()
    {
        return _context.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.CreatedByUser)
            .Include(r => r.PurchaseOrder)
                .ThenInclude(p => p.Supplier)
            .Include(r => r.PurchaseOrder)
                .ThenInclude(p => p.MaterialRequest)
                    .ThenInclude(r => r.Project)
            .Include(r => r.Lines)
                .ThenInclude(l => l.PurchaseOrderLine)
                    .ThenInclude(l => l.MaterialRequestLine)
                        .ThenInclude(l => l.Material);
    }

    [HttpGet]
    public async Task<ActionResult<List<GoodsReceiptResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var receipts = await ReceiptQuery()
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(receipts.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GoodsReceiptResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var receipt = await ReceiptQuery()
            .FirstOrDefaultAsync(
                r => r.Id == id,
                cancellationToken);

        if (receipt is null)
        {
            return NotFound(new
            {
                message = "Goods receipt not found."
            });
        }

        return Ok(ToResponse(receipt));
    }

    [HttpGet("receivable-orders")]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper)]
    public async Task<ActionResult<List<ReceivableOrderResponse>>>
        GetReceivableOrders(CancellationToken cancellationToken)
    {
        var orders = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.MaterialRequest)
                .ThenInclude(r => r.Project)
            .Include(p => p.Lines)
                .ThenInclude(l => l.MaterialRequestLine)
                    .ThenInclude(l => l.Material)
            .Where(p =>
                p.Status == "Issued" ||
                p.Status == "PartiallyReceived")
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        var orderIds = orders
            .Select(p => p.Id)
            .ToList();

        var receivedQuantities = await _context.GoodsReceiptLines
            .Where(l =>
                orderIds.Contains(l.GoodsReceipt.PurchaseOrderId) &&
                l.GoodsReceipt.Status == "Posted")
            .GroupBy(l => l.PurchaseOrderLineId)
            .Select(group => new
            {
                Id = group.Key,
                Quantity = group.Sum(l => l.Quantity)
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Quantity,
                cancellationToken);

        var result = new List<ReceivableOrderResponse>();

        foreach (var order in orders)
        {
            var lines = new List<ReceivableOrderLineResponse>();

            foreach (var line in order.Lines)
            {
                var received =
                    receivedQuantities.GetValueOrDefault(line.Id);

                var remaining = line.Quantity - received;

                if (remaining <= 0)
                {
                    continue;
                }

                var material = line.MaterialRequestLine.Material;

                lines.Add(new ReceivableOrderLineResponse
                {
                    PurchaseOrderLineId = line.Id,
                    MaterialCode = material.Code,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    OrderedQuantity = line.Quantity,
                    ReceivedQuantity = received,
                    RemainingQuantity = remaining,
                    UnitCost = line.UnitPrice
                });
            }

            if (lines.Count > 0)
            {
                result.Add(new ReceivableOrderResponse
                {
                    Id = order.Id,
                    OrderNumber = $"PO-{order.Id:D6}",
                    ProjectName = order.MaterialRequest.Project.Name,
                    SupplierName = order.Supplier.Name,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    Lines = lines
                });
            }
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper)]
    public async Task<ActionResult<GoodsReceiptResponse>> Create(
        CreateGoodsReceiptDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var userExists = await _context.Users.AnyAsync(
            u => u.Id == userId,
            cancellationToken);

        if (!userExists)
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(dto.ClientRequestId, out var requestGuid) ||
            requestGuid == Guid.Empty)
        {
            return BadRequest(new
            {
                message =
                    "ClientRequestId must be a valid nonempty UUID."
            });
        }

        var requestKey = requestGuid.ToString("D");

        if (dto.ReceivedDate == default)
        {
            return BadRequest(new
            {
                message = "Received date is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.DeliveryNoteNumber))
        {
            return BadRequest(new
            {
                message = "Delivery note number is required."
            });
        }

        if (dto.Lines is null ||
            dto.Lines.Count == 0 ||
            dto.Lines.Count > 100 ||
            dto.Lines.Any(l => l is null))
        {
            return BadRequest(new
            {
                message =
                    "Add between 1 and 100 valid receipt lines."
            });
        }

        var lineIds = dto.Lines
            .Select(l => l.PurchaseOrderLineId)
            .ToList();

        if (lineIds.Distinct().Count() != lineIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "Each purchase order line can appear only once."
            });
        }

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0 ||
                line.Quantity > 999999999.999m ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return BadRequest(new
                {
                    message =
                        "Quantities must be positive, within range, and have at most 3 decimal places."
                });
            }
        }

        // Return the original receipt if a completed submission is retried.
        var existing = await ReceiptQuery()
            .FirstOrDefaultAsync(
                r => r.ClientRequestId == requestKey,
                cancellationToken);

        if (existing is not null)
        {
            return ReplayResult(existing, dto, userId);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Prevent simultaneous deliveries from exceeding order quantities.
        // The inventory import uses this same purchase order lock.
        var lockedOrders = await _context.PurchaseOrders
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM `PurchaseOrders`
                WHERE `Id` = {dto.PurchaseOrderId}
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var order = lockedOrders.SingleOrDefault();

        if (order is null)
        {
            return BadRequest(new
            {
                message = "Purchase order not found."
            });
        }

        var projectId = await _context.MaterialRequests.Where(r => r.Id == order.MaterialRequestId)
            .Select(r => r.ProjectId).SingleAsync(cancellationToken);
        if (!await OperationRules.LockProject(_context, projectId))
            return BadRequest(new { message = "The project must be active and assigned to you." });

        // Recheck after waiting for the purchase order lock.
        existing = await ReceiptQuery()
            .FirstOrDefaultAsync(
                r => r.ClientRequestId == requestKey,
                cancellationToken);

        if (existing is not null)
        {
            return ReplayResult(existing, dto, userId);
        }

        if (order.Status != "Issued" &&
            order.Status != "PartiallyReceived")
        {
            return BadRequest(new
            {
                message =
                    "Only issued or partially received orders can receive deliveries."
            });
        }

        if (dto.ReceivedDate < order.OrderDate)
        {
            return BadRequest(new
            {
                message =
                    "Received date cannot be before the purchase order date."
            });
        }

        var orderLines = await _context.PurchaseOrderLines
            .AsNoTracking()
            .Include(l => l.MaterialRequestLine)
            .Where(l => l.PurchaseOrderId == order.Id)
            .ToDictionaryAsync(
                l => l.Id,
                cancellationToken);

        if (lineIds.Any(id => !orderLines.ContainsKey(id)))
        {
            return BadRequest(new
            {
                message =
                    "Every receipt line must belong to the selected purchase order."
            });
        }

        var receivedQuantities = await _context.GoodsReceiptLines
            .Where(l =>
                l.GoodsReceipt.PurchaseOrderId == order.Id &&
                l.GoodsReceipt.Status == "Posted")
            .GroupBy(l => l.PurchaseOrderLineId)
            .Select(group => new
            {
                Id = group.Key,
                Quantity = group.Sum(l => l.Quantity)
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Quantity,
                cancellationToken);

        foreach (var line in dto.Lines)
        {
            var ordered = orderLines[line.PurchaseOrderLineId];

            var alreadyReceived =
                receivedQuantities.GetValueOrDefault(
                    line.PurchaseOrderLineId);

            var remaining = ordered.Quantity - alreadyReceived;

            if (line.Quantity > remaining)
            {
                return Conflict(new
                {
                    message =
                        $"Purchase order line {line.PurchaseOrderLineId} has only {remaining} remaining."
                });
            }
        }

        var materialIds = dto.Lines.Select(l => orderLines[l.PurchaseOrderLineId].MaterialRequestLine.MaterialId).ToList();
        var latestDates = await _context.StockMovements
            .Where(m => m.ProjectId == projectId && materialIds.Contains(m.MaterialId))
            .GroupBy(m => m.MaterialId).Select(g => g.Max(m => m.MovementDate))
            .ToListAsync(cancellationToken);
        if (latestDates.Any(date => dto.ReceivedDate < date))
            return BadRequest(new { message = "Received date cannot precede the latest stock movement for a delivered material." });

        var receipt = new GoodsReceipt
        {
            ClientRequestId = requestKey,
            PurchaseOrderId = order.Id,
            ReceivedDate = dto.ReceivedDate,
            DeliveryNoteNumber = dto.DeliveryNoteNumber.Trim(),
            Notes = dto.Notes?.Trim() ?? string.Empty,
            Status = "Posted",
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,

            Lines = dto.Lines.Select(line => new GoodsReceiptLine
            {
                PurchaseOrderLineId = line.PurchaseOrderLineId,
                Quantity = line.Quantity,

                // Always copy the price from the purchase order.
                UnitCost =
                    orderLines[line.PurchaseOrderLineId].UnitPrice
            }).ToList()
        };

        foreach (var line in dto.Lines)
        {
            receivedQuantities[line.PurchaseOrderLineId] =
                receivedQuantities.GetValueOrDefault(
                    line.PurchaseOrderLineId) + line.Quantity;
        }

        var fullyReceived = orderLines.Values.All(line =>
            receivedQuantities.GetValueOrDefault(line.Id)
                >= line.Quantity);

        order.Status = fullyReceived
            ? "Received"
            : "PartiallyReceived";

        _context.GoodsReceipts.Add(receipt);

        try
        {
            // Generate receipt and receipt-line IDs.
            await _context.SaveChangesAsync(cancellationToken);

            // Create missing stock movements inside the same transaction.
            await ReceiptStockPosting.PostMissingAsync(
                _context,
                order.Id,
                cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transaction.RollbackAsync(cancellationToken);

            // Remove entities from the failed transaction.
            _context.ChangeTracker.Clear();

            var duplicate = await ReceiptQuery()
                .FirstOrDefaultAsync(
                    r => r.ClientRequestId == requestKey,
                    cancellationToken);

            if (duplicate is not null)
            {
                return ReplayResult(duplicate, dto, userId);
            }

            return Conflict(new
            {
                message =
                    "A duplicate receipt or stock posting was detected. Reload and try again."
            });
        }

        // Both the receipt and stock movements succeed together.
        await transaction.CommitAsync(cancellationToken);

        var saved = await ReceiptQuery()
            .SingleAsync(
                r => r.Id == receipt.Id,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = receipt.Id },
            ToResponse(saved));
    }

    private ActionResult<GoodsReceiptResponse> ReplayResult(
        GoodsReceipt existing,
        CreateGoodsReceiptDto dto,
        string userId)
    {
        var sameHeader =
            existing.CreatedByUserId == userId &&
            existing.PurchaseOrderId == dto.PurchaseOrderId &&
            existing.ReceivedDate == dto.ReceivedDate &&
            existing.DeliveryNoteNumber ==
                dto.DeliveryNoteNumber.Trim() &&
            existing.Notes == (dto.Notes?.Trim() ?? string.Empty);

        var sameLines =
            existing.Lines.Count == dto.Lines.Count &&
            existing.Lines.All(saved =>
                dto.Lines.Any(incoming =>
                    incoming.PurchaseOrderLineId ==
                        saved.PurchaseOrderLineId &&
                    incoming.Quantity == saved.Quantity));

        if (!sameHeader || !sameLines)
        {
            return Conflict(new
            {
                message =
                    "This submission ID has already been used for different receipt data."
            });
        }

        return Ok(ToResponse(existing));
    }

    private static GoodsReceiptResponse ToResponse(
        GoodsReceipt receipt)
    {
        var lines = receipt.Lines
            .OrderBy(l => l.Id)
            .Select(line =>
            {
                var material = line.PurchaseOrderLine
                    .MaterialRequestLine.Material;

                return new GoodsReceiptLineResponse
                {
                    MaterialId = material.Id,
                    Id = line.Id,
                    PurchaseOrderLineId = line.PurchaseOrderLineId,
                    MaterialCode = material.Code,
                    MaterialName = material.Name,
                    Unit = material.Unit,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,

                    LineTotal = decimal.Round(
                        line.Quantity * line.UnitCost,
                        2,
                        MidpointRounding.AwayFromZero)
                };
            })
            .ToList();

        return new GoodsReceiptResponse
        {
            ProjectId = receipt.PurchaseOrder.MaterialRequest.ProjectId,
            Id = receipt.Id,
            ReceiptNumber = $"GR-{receipt.Id:D6}",
            ClientRequestId = receipt.ClientRequestId,
            PurchaseOrderId = receipt.PurchaseOrderId,
            OrderNumber = $"PO-{receipt.PurchaseOrderId:D6}",

            ProjectName = receipt.PurchaseOrder
                .MaterialRequest.Project.Name,

            SupplierName = receipt.PurchaseOrder.Supplier.Name,
            ReceivedDate = receipt.ReceivedDate,
            DeliveryNoteNumber = receipt.DeliveryNoteNumber,
            Notes = receipt.Notes,
            Status = receipt.Status,
            CreatedByName = receipt.CreatedByUser.FullName,
            CreatedAtUtc = receipt.CreatedAtUtc,
            TotalValue = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}
