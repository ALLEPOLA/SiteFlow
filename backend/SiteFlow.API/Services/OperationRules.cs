using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;

namespace SiteFlow.API.Services;

public static class OperationRules
{
    public static decimal Money(decimal quantity, decimal cost) => decimal.Round(quantity * cost, 2, MidpointRounding.AwayFromZero);
    public static bool Quantity(decimal value) => Math.Abs(value) <= 999999999.999m && decimal.Round(value, 3) == value;
    public static bool Price(decimal value) => value >= 0 && value <= 999999999.99m && decimal.Round(value, 2) == value;
    public static async Task<bool> LockProject(AppDbContext db, int id)
    {
        var projects = await db.Projects.FromSqlInterpolated($"SELECT * FROM `Projects` WHERE `Id` = {id} FOR UPDATE").ToListAsync();
        return projects.Any(p => p.Status == "Active");
    }

    public static async Task<(decimal Quantity, decimal Cost, DateOnly Latest)> Stock(AppDbContext db, int projectId, int materialId, int? storageLocationId = null)
    {
        var rows = await db.StockMovements.Where(m => m.ProjectId == projectId && m.MaterialId == materialId && (!storageLocationId.HasValue || m.StorageLocationId == storageLocationId))
            .Select(m => new { m.Quantity, m.UnitCost, m.MovementDate }).ToListAsync();
        var quantity = rows.Sum(m => m.Quantity);
        var value = rows.Sum(m => m.Quantity * m.UnitCost);
        var cost = quantity > 0 ? decimal.Round(Math.Max(0, value / quantity), 2, MidpointRounding.AwayFromZero) : 0;
        return (quantity, cost, rows.Count > 0 ? rows.Max(m => m.MovementDate) : DateOnly.MinValue);
    }

    public static StockMovement Movement(AppDbContext db, int project, int material, string type, decimal quantity, decimal cost, DateOnly date, int storageLocationId) => new()
    {
        StorageLocationId = storageLocationId, ProjectId = project, MaterialId = material, MovementType = type,
        Quantity = quantity, UnitCost = cost, MovementDate = date,
        CreatedByUserId = db.CurrentUserId, CreatedAtUtc = DateTime.UtcNow
    };
}
