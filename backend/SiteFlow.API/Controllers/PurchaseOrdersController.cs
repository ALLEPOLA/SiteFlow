using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.PurchaseOrders;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly AppDbContext _context;

    public PurchaseOrdersController(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<PurchaseOrder> OrderQuery()
    {
        return _context.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.CreatedByUser)
            .Include(p => p.MaterialRequest)
                .ThenInclude(r => r.Project)
            .Include(p => p.Lines)
                .ThenInclude(l => l.MaterialRequestLine)
                    .ThenInclude(l => l.Material);
    }

    [HttpGet]
    public async Task<ActionResult<List<PurchaseOrderResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var orders = await OrderQuery()
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await OrderQuery()
            .FirstOrDefaultAsync(
                p => p.Id == id,
                cancellationToken);

        if (order is null)
        {
            return NotFound(new
            {
                message = "Purchase order not found."
            });
        }

        return Ok(ToResponse(order));
    }

    [HttpGet("available-requests")]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<ActionResult<List<AvailableRequestResponse>>>
        GetAvailableRequests(CancellationToken cancellationToken)
    {
        var requests = await _context.MaterialRequests
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Material)
            .Where(r =>
                r.Status == "Approved" &&
                r.Project.Status == "Active")
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        var requestIds = requests
            .Select(r => r.Id)
            .ToList();

        var orderedQuantities = await _context.PurchaseOrderLines
            .Where(l =>
                requestIds.Contains(
                    l.PurchaseOrder.MaterialRequestId) &&
                l.PurchaseOrder.Status != "Cancelled")
            .GroupBy(l => l.MaterialRequestLineId)
            .Select(group => new
            {
                MaterialRequestLineId = group.Key,
                Quantity = group.Sum(l => l.Quantity)
            })
            .ToDictionaryAsync(
                item => item.MaterialRequestLineId,
                item => item.Quantity,
                cancellationToken);

        var result = new List<AvailableRequestResponse>();

        foreach (var request in requests)
        {
            var availableLines =
                new List<AvailableRequestLineResponse>();

            foreach (var line in request.Lines)
            {
                var ordered = orderedQuantities.GetValueOrDefault(
                    line.Id);

                var remaining = line.Quantity - ordered;

                if (remaining <= 0 || !line.Material.IsActive)
                {
                    continue;
                }

                availableLines.Add(new AvailableRequestLineResponse
                {
                    MaterialRequestLineId = line.Id,
                    MaterialCode = line.Material.Code,
                    MaterialName = line.Material.Name,
                    Unit = line.Material.Unit,
                    RequestedQuantity = line.Quantity,
                    OrderedQuantity = ordered,
                    RemainingQuantity = remaining,
                    EstimatedUnitPrice = line.EstimatedUnitPrice
                });
            }

            if (availableLines.Count > 0)
            {
                result.Add(new AvailableRequestResponse
                {
                    Id = request.Id,
                    RequestNumber = $"MR-{request.Id:D6}",
                    ProjectName = request.Project.Name,
                    RequiredDate = request.RequiredDate,
                    Lines = availableLines
                });
            }
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<ActionResult<PurchaseOrderResponse>> Create(
        CreatePurchaseOrderDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var creatorExists = await _context.Users
            .AnyAsync(
                u => u.Id == userId,
                cancellationToken);

        if (!creatorExists)
        {
            return Unauthorized();
        }

        if (dto.OrderDate == default ||
            dto.ExpectedDeliveryDate == default)
        {
            return BadRequest(new
            {
                message = "Order date and expected delivery date are required."
            });
        }

        if (dto.ExpectedDeliveryDate < dto.OrderDate)
        {
            return BadRequest(new
            {
                message =
                    "Expected delivery date cannot be before the order date."
            });
        }

        if (dto.Lines is null ||
            dto.Lines.Count == 0 ||
            dto.Lines.Count > 100 ||
            dto.Lines.Any(l => l is null))
        {
            return BadRequest(new
            {
                message = "Add between 1 and 100 valid order lines."
            });
        }

        var requestedLineIds = dto.Lines
            .Select(l => l.MaterialRequestLineId)
            .ToList();

        if (requestedLineIds.Distinct().Count() !=
            requestedLineIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "Each request line can appear only once per purchase order."
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

            if (line.UnitPrice < 0 ||
                line.UnitPrice > 999999999.99m ||
                decimal.Round(line.UnitPrice, 2) != line.UnitPrice)
            {
                return BadRequest(new
                {
                    message =
                        "Prices must be nonnegative, within range, and have at most 2 decimal places."
                });
            }
        }

        // All order creation for the same request is serialized
        // by locking the request header.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        var lockedRequests = await _context.MaterialRequests
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM `MaterialRequests`
                WHERE `Id` = {dto.MaterialRequestId}
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var request = lockedRequests.SingleOrDefault();

        if (request is null)
        {
            return BadRequest(new
            {
                message = "Material request not found."
            });
        }

        if (request.Status != "Approved")
        {
            return BadRequest(new
            {
                message =
                    "Purchase orders require an approved material request."
            });
        }

        var activeProject = await _context.Projects
            .AnyAsync(
                p => p.Id == request.ProjectId &&
                     p.Status == "Active",
                cancellationToken);

        if (!activeProject)
        {
            return BadRequest(new
            {
                message = "The request's project must be active."
            });
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == dto.SupplierId,
                cancellationToken);

        if (supplier is null || !supplier.IsActive)
        {
            return BadRequest(new
            {
                message = "Select an existing active supplier."
            });
        }

        var approvedLines = await _context.MaterialRequestLines
            .AsNoTracking()
            .Include(l => l.Material)
            .Where(l =>
                l.MaterialRequestId == request.Id &&
                requestedLineIds.Contains(l.Id))
            .ToDictionaryAsync(
                l => l.Id,
                cancellationToken);

        if (approvedLines.Count != requestedLineIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "Every order line must belong to the selected material request."
            });
        }

        if (approvedLines.Values.Any(l => !l.Material.IsActive))
        {
            return BadRequest(new
            {
                message = "Only active materials can be ordered."
            });
        }

        // Draft orders reserve quantities too.
        var orderedQuantities = await _context.PurchaseOrderLines
            .Where(l =>
                l.PurchaseOrder.MaterialRequestId == request.Id &&
                l.PurchaseOrder.Status != "Cancelled")
            .GroupBy(l => l.MaterialRequestLineId)
            .Select(group => new
            {
                MaterialRequestLineId = group.Key,
                Quantity = group.Sum(l => l.Quantity)
            })
            .ToDictionaryAsync(
                item => item.MaterialRequestLineId,
                item => item.Quantity,
                cancellationToken);

        foreach (var line in dto.Lines)
        {
            var approvedLine =
                approvedLines[line.MaterialRequestLineId];

            var alreadyOrdered =
                orderedQuantities.GetValueOrDefault(
                    line.MaterialRequestLineId);

            var remaining =
                approvedLine.Quantity - alreadyOrdered;

            if (line.Quantity > remaining)
            {
                return Conflict(new
                {
                    message =
                        $"Only {remaining} {approvedLine.Material.Unit} remain available for {approvedLine.Material.Name}."
                });
            }
        }

        var order = new PurchaseOrder
        {
            MaterialRequestId = request.Id,
            SupplierId = supplier.Id,
            OrderDate = dto.OrderDate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Notes = dto.Notes?.Trim() ?? string.Empty,
            Status = "Draft",
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,

            Lines = dto.Lines.Select(line => new PurchaseOrderLine
            {
                MaterialRequestLineId =
                    line.MaterialRequestLineId,

                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            }).ToList()
        };

        _context.PurchaseOrders.Add(order);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var savedOrder = await OrderQuery()
            .SingleAsync(
                p => p.Id == order.Id,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            ToResponse(savedOrder));
    }

    private static PurchaseOrderResponse ToResponse(
        PurchaseOrder order)
    {
        var lines = order.Lines
            .OrderBy(l => l.Id)
            .Select(line => new PurchaseOrderLineResponse
            {
                Id = line.Id,

                MaterialRequestLineId =
                    line.MaterialRequestLineId,

                MaterialId =
                    line.MaterialRequestLine.MaterialId,

                MaterialCode =
                    line.MaterialRequestLine.Material.Code,

                MaterialName =
                    line.MaterialRequestLine.Material.Name,

                Unit =
                    line.MaterialRequestLine.Material.Unit,

                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,

                LineTotal = decimal.Round(
                    line.Quantity * line.UnitPrice,
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        return new PurchaseOrderResponse
        {
            Id = order.Id,
            OrderNumber = $"PO-{order.Id:D6}",
            MaterialRequestId = order.MaterialRequestId,
            RequestNumber = $"MR-{order.MaterialRequestId:D6}",
            ProjectName = order.MaterialRequest.Project.Name,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier.Name,
            OrderDate = order.OrderDate,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            Notes = order.Notes,
            Status = order.Status,
            CreatedByName = order.CreatedByUser.FullName,
            CreatedAtUtc = order.CreatedAtUtc,
            TotalAmount = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}