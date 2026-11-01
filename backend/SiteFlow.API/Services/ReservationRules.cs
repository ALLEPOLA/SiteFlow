using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;

namespace SiteFlow.API.Services;

public static class ReservationRules
{
    public static decimal Remaining(decimal quantity, decimal consumed, decimal released) =>
        Math.Max(0, quantity - consumed - released);
    public static decimal Available(decimal onHand, decimal reserved) => Math.Max(0, onHand - reserved);
    public static bool CanReduceTo(decimal balanceAfter, decimal reserved) => balanceAfter >= 0 && balanceAfter >= reserved;
    public static Task<decimal> Held(AppDbContext db, int projectId, int materialId, CancellationToken ct = default) =>
        db.MaterialReservations.Where(r => r.ProjectId == projectId && r.MaterialId == materialId)
            .SumAsync(r => r.Quantity - r.ConsumedQuantity - r.ReleasedQuantity, ct);
}
