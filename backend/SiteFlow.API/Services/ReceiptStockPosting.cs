using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;

namespace SiteFlow.API.Services;

public static class ReceiptStockPosting
{
    // Call inside a transaction after locking the purchase order.
    public static async Task<int> PostMissingAsync(
        AppDbContext context,
        int purchaseOrderId,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Stock posting requires a database transaction.");
        }

        var missingLines = await context.GoodsReceiptLines
            .AsNoTracking()
            .Where(line =>
                line.GoodsReceipt.PurchaseOrderId == purchaseOrderId &&
                line.GoodsReceipt.Status == "Posted" &&
                !context.StockMovements.Any(movement =>
                    movement.GoodsReceiptLineId == line.Id))
            .Select(line => new
            {
                ReceiptLineId = line.Id,
                line.GoodsReceipt.StorageLocationId,

                ProjectId = line.GoodsReceipt.PurchaseOrder
                    .MaterialRequest.ProjectId,

                MaterialId = line.PurchaseOrderLine
                    .MaterialRequestLine.MaterialId,

                line.Quantity,
                line.UnitCost,

                MovementDate = line.GoodsReceipt.ReceivedDate,

                CreatedByUserId =
                    line.GoodsReceipt.CreatedByUserId,

                CreatedAtUtc =
                    line.GoodsReceipt.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        foreach (var line in missingLines)
        {
            context.StockMovements.Add(new StockMovement
            {
                ProjectId = line.ProjectId,
                StorageLocationId = line.StorageLocationId,
                MaterialId = line.MaterialId,
                MovementType = "Receipt",
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                MovementDate = line.MovementDate,
                GoodsReceiptLineId = line.ReceiptLineId,
                CreatedByUserId = line.CreatedByUserId,
                CreatedAtUtc = line.CreatedAtUtc
            });
        }

        if (missingLines.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return missingLines.Count;
    }
}