using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        CancellationToken cancellationToken)
    {
        var activeProjects = await _context.Projects
            .AsNoTracking()
            .CountAsync(
                p => p.Status == "Active",
                cancellationToken);

        var pendingRequests = await _context.MaterialRequests
            .AsNoTracking()
            .CountAsync(
                r => r.Status == "Submitted",
                cancellationToken);

        // Draft purchase orders also reserve request quantities.
        // Count each approved request once if any active material
        // still has quantity available to order.
        var requestsAwaitingPurchase =
            await _context.MaterialRequests
                .AsNoTracking()
                .Where(r =>
                    r.Status == "Approved" &&
                    r.Project.Status == "Active")
                .CountAsync(
                    r => r.Lines.Any(line =>
                        line.Material.IsActive &&
                        line.Quantity >
                            (
                                _context.PurchaseOrderLines
                                    .Where(orderLine =>
                                        orderLine.MaterialRequestLineId ==
                                            line.Id &&
                                        orderLine.PurchaseOrder.Status !=
                                            "Cancelled")
                                    .Sum(orderLine =>
                                        (decimal?)orderLine.Quantity)
                                ?? 0m
                            )),
                    cancellationToken);

        var ordersAwaitingDelivery = await _context.PurchaseOrders
            .AsNoTracking()
            .CountAsync(
                p =>
                    p.Status == "Issued" ||
                    p.Status == "PartiallyReceived",
                cancellationToken);

        var draftPurchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .CountAsync(
                p => p.Status == "Draft",
                cancellationToken);

        var postedReceipts = await _context.GoodsReceipts
            .AsNoTracking()
            .CountAsync(
                r => r.Status == "Posted",
                cancellationToken);

        var postedMaterialIssues = await _context.MaterialIssues
            .AsNoTracking()
            .CountAsync(
                i => i.Status == "Posted",
                cancellationToken);

        var recentReceipts = await _context.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.Status == "Posted")
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Take(5)
            .Select(r => new
            {
                r.Id,
                r.PurchaseOrderId,

                ProjectName = r.PurchaseOrder
                    .MaterialRequest.Project.Name,

                SupplierName = r.PurchaseOrder.Supplier.Name,

                r.ReceivedDate,
                r.DeliveryNoteNumber,

                CreatedByName = r.CreatedByUser.FullName,

                r.CreatedAtUtc,
                LineCount = r.Lines.Count
            })
            .ToListAsync(cancellationToken);

        var recentIssues = await _context.MaterialIssues
            .AsNoTracking()
            .Where(i => i.Status == "Posted")
            .OrderByDescending(i => i.CreatedAtUtc)
            .ThenByDescending(i => i.Id)
            .Take(5)
            .Select(i => new
            {
                i.Id,
                ProjectName = i.Project.Name,
                i.IssueDate,
                i.Purpose,
                i.ReceivedBy,

                CreatedByName = i.CreatedByUser.FullName,

                i.CreatedAtUtc,
                LineCount = i.Lines.Count
            })
            .ToListAsync(cancellationToken);

        var requestsToReview = await _context.MaterialRequests
            .AsNoTracking()
            .Where(r => r.Status == "Submitted")
            .OrderBy(r => r.SubmittedAtUtc)
            .ThenBy(r => r.Id)
            .Take(5)
            .Select(r => new
            {
                r.Id,
                ProjectName = r.Project.Name,
                r.RequiredDate,

                CreatedByName = r.CreatedByUser == null
                    ? "Unknown"
                    : r.CreatedByUser.FullName,

                r.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);

        var outstandingDeliveries = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(p =>
                p.Status == "Issued" ||
                p.Status == "PartiallyReceived")
            .OrderBy(p => p.ExpectedDeliveryDate)
            .ThenBy(p => p.Id)
            .Take(5)
            .Select(p => new
            {
                p.Id,

                ProjectName = p.MaterialRequest.Project.Name,

                SupplierName = p.Supplier.Name,

                p.ExpectedDeliveryDate,
                p.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,

            summary = new
            {
                activeProjects,
                pendingRequests,
                requestsAwaitingPurchase,
                draftPurchaseOrders,
                ordersAwaitingDelivery,
                postedReceipts,
                postedMaterialIssues
            },

            recentReceipts,
            recentIssues,
            requestsToReview,
            outstandingDeliveries
        });
    }
}