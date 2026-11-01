using SiteFlow.API.Security;
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
        Response.Headers["Cache-Control"] = "no-store";
        var allowedPages = WorkspacePermissions.Pages(User);
        var reviewer = WorkspacePermissions.CanReview(User);
        var engineer = User.IsInRole(AppRoles.SiteEngineer);
        var purchaser = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.ProcurementOfficer);
        var userId = _context.CurrentUserId;
        var showReceipts = allowedPages.Contains("receipts");
        var showIssues = allowedPages.Contains("issues");
        var showOrders = allowedPages.Contains("orders");
        var activeReservations = await _context.MaterialReservations.CountAsync(r => r.Quantity > r.ConsumedQuantity + r.ReleasedQuantity, cancellationToken);
        var activeProjects = await _context.Projects
            .AsNoTracking()
            .CountAsync(
                p => p.Status == "Active",
                cancellationToken);

        var pendingRequests = await _context.MaterialRequests
            .AsNoTracking()
            .CountAsync(
                r => r.Status == "Submitted" && (reviewer
                    ? r.CreatedByUserId != null && r.CreatedByUserId != userId
                    : engineer && r.CreatedByUserId == userId),
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
            .Where(r => showReceipts && r.Status == "Posted")
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
            .Where(i => showIssues && i.Status == "Posted")
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
            .Where(r => reviewer && r.Status == "Submitted" && r.CreatedByUserId != null && r.CreatedByUserId != userId)
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
            .Where(p => showOrders && showReceipts &&
                (p.Status == "Issued" || p.Status == "PartiallyReceived"))
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
            allowedPages,

            summary = new
            {
                activeProjects,
                activeReservations,
                pendingRequests = reviewer || engineer ? (int?)pendingRequests : null,
                requestsAwaitingPurchase = purchaser ? (int?)requestsAwaitingPurchase : null,
                draftPurchaseOrders = purchaser ? (int?)draftPurchaseOrders : null,
                ordersAwaitingDelivery = showReceipts ? (int?)ordersAwaitingDelivery : null,
                postedReceipts = showReceipts ? (int?)postedReceipts : null,
                postedMaterialIssues = showIssues ? (int?)postedMaterialIssues : null
            },

            recentReceipts,
            recentIssues,
            requestsToReview,
            outstandingDeliveries
        });
    }
}