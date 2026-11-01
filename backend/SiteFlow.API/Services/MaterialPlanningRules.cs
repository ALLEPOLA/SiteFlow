using SiteFlow.API.Models;

namespace SiteFlow.API.Services;

public record PlanningDelivery(DateOnly ExpectedDate, decimal Quantity);
public record PlanningCalculation(decimal Stock, decimal Incoming, decimal OverdueIncoming,
    decimal LateIncoming, decimal Shortage, decimal EstimatedCost, DateOnly OrderByDate, bool OrderIsLate);

public static class MaterialPlanningRules
{
    // Quantities are in each material's own unit. An overdue promise is not reliable supply.
    public static PlanningCalculation Calculate(decimal demand, decimal safetyStock, decimal stock,
        IEnumerable<PlanningDelivery> deliveries, DateOnly today, DateOnly requiredDate,
        int leadTimeDays, decimal estimatedUnitPrice)
    {
        var rows = deliveries.ToList();
        var available = Math.Max(0, stock);
        var incoming = rows.Where(d => d.ExpectedDate >= today && d.ExpectedDate <= requiredDate)
            .Sum(d => Math.Max(0, d.Quantity));
        var overdue = rows.Where(d => d.ExpectedDate < today).Sum(d => Math.Max(0, d.Quantity));
        var late = rows.Where(d => d.ExpectedDate > requiredDate).Sum(d => Math.Max(0, d.Quantity));
        var shortage = Math.Max(0, demand + safetyStock - available - incoming);
        var orderBy = requiredDate.AddDays(-leadTimeDays);
        return new(available, incoming, overdue, late, shortage,
            OperationRules.Money(shortage, estimatedUnitPrice), orderBy, shortage > 0 && orderBy < today);
    }
}
