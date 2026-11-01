using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/stores")]
public class StoresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? projectId, CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-store";
        if (projectId is <= 0) return BadRequest();
        return Ok(await db.Stores.AsNoTracking().Where(s => !projectId.HasValue || s.ProjectId == projectId)
            .OrderBy(s => s.Project.Code).ThenBy(s => s.Code).Select(s => new { s.Id, s.ProjectId,
                projectName = s.Project.Name, s.Code, s.Name, s.IsActive, locationCount = s.Locations.Count }).ToListAsync(ct));
    }
    [HttpGet("locations")]
    public async Task<IActionResult> Locations([FromQuery] int? projectId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        Response.Headers["Cache-Control"] = "no-store";
        if (projectId is <= 0) return BadRequest();
        return Ok(await db.StorageLocations.AsNoTracking().Where(l => (!projectId.HasValue || l.Store.ProjectId == projectId) &&
            (!activeOnly || (l.IsActive && l.Store.IsActive)))
            .OrderBy(l => l.Store.Project.Code).ThenBy(l => l.Store.Code).ThenBy(l => l.Code)
            .Select(l => new { l.Id, l.StoreId, l.Store.ProjectId, projectName = l.Store.Project.Name,
                storeCode = l.Store.Code, storeName = l.Store.Name, l.Code, l.Name, l.Rack, l.Bin, l.IsActive, storeActive = l.Store.IsActive }).ToListAsync(ct));
    }
    [HttpPost, Authorize(Roles = "Administrator,Storekeeper")]
    public async Task<IActionResult> Create(StoreDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await OperationRules.LockProject(db, dto.ProjectId)) return BadRequest(new { message = "Choose an active assigned project." });
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await db.Stores.AnyAsync(s => s.ProjectId == dto.ProjectId && s.Code == code, ct))
            return Conflict(new { message = "Store code already exists in this project." });
        var store = new Store { ProjectId = dto.ProjectId, Code = code, Name = dto.Name.Trim(),
            Locations = new() { new StorageLocation { Code = "DEFAULT", Name = "Default Location" } } };
        db.Stores.Add(store); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return StatusCode(201, new { store.Id, message = "Store created with a default storage location." });
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Administrator,Storekeeper")]
    public async Task<IActionResult> EditStore(int id, EditStoreDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest();
        var projectId = await db.Stores.Where(s => s.Id == id).Select(s => (int?)s.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await OperationRules.LockProject(db, projectId.Value)) return BadRequest(new { message = "Project must be active." });
        var store = await db.Stores.SingleAsync(s => s.Id == id, ct);
        if (!dto.IsActive)
        {
            if (store.Code == "MAIN") return Conflict(new { message = "The system Main Store remains active for existing clients." });
            var balances = await db.StockMovements.Where(m => m.StorageLocation.StoreId == id)
                .GroupBy(m => new { m.StorageLocationId, m.MaterialId }).Select(g => g.Sum(m => m.Quantity)).ToListAsync(ct);
            if (balances.Any(q => q != 0) || await db.MaterialReservations.AnyAsync(r => r.StorageLocation.StoreId == id && r.Quantity > r.ConsumedQuantity + r.ReleasedQuantity, ct))
                return Conflict(new { message = "Move all stock and release reservations before deactivating a store." });
        }
        store.Name = dto.Name.Trim(); store.IsActive = dto.IsActive;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { message = "Store updated. Location and transaction history is preserved." });
    }

    [HttpPost("locations"), Authorize(Roles = "Administrator,Storekeeper")]
    public async Task<IActionResult> CreateLocation(LocationDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest();
        var projectId = await db.Stores.Where(s => s.Id == dto.StoreId).Select(s => (int?)s.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await OperationRules.LockProject(db, projectId.Value)) return BadRequest(new { message = "Project must be active." });
        if (!await db.Stores.AnyAsync(s => s.Id == dto.StoreId && s.IsActive, ct)) return BadRequest(new { message = "Choose an active store." });
        var code = dto.Code.Trim().ToUpperInvariant();
        if (await db.StorageLocations.AnyAsync(l => l.StoreId == dto.StoreId && l.Code == code, ct))
            return Conflict(new { message = "Location code already exists in this store." });
        var location = new StorageLocation { StoreId = dto.StoreId, Code = code, Name = dto.Name.Trim(), Rack = dto.Rack.Trim(), Bin = dto.Bin.Trim() };
        db.StorageLocations.Add(location); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return StatusCode(201, new { location.Id, message = "Storage location created." });
    }
    [HttpPut("locations/{id:int}"), Authorize(Roles = "Administrator,Storekeeper")]
    public async Task<IActionResult> EditLocation(int id, EditLocationDto dto, CancellationToken ct)
    {
        var projectId = await db.StorageLocations.Where(l => l.Id == id).Select(l => (int?)l.Store.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await OperationRules.LockProject(db, projectId.Value)) return BadRequest(new { message = "Project must be active." });
        var location = await db.StorageLocations.Include(l => l.Store).SingleAsync(l => l.Id == id, ct);
        if (!dto.IsActive)
        {
            if (location.Store.Code == "MAIN" && location.Code == "DEFAULT")
                return Conflict(new { message = "The system default remains active for existing clients." });
            var balances = await db.StockMovements.Where(m => m.StorageLocationId == id).GroupBy(m => m.MaterialId)
                .Select(g => g.Sum(m => m.Quantity)).ToListAsync(ct);
            if (balances.Any(q => q != 0) || await db.MaterialReservations.AnyAsync(r => r.StorageLocationId == id && r.Quantity > r.ConsumedQuantity + r.ReleasedQuantity, ct))
                return Conflict(new { message = "Move remaining stock and release reservations before deactivating a location." });
        }
        location.Name = dto.Name.Trim(); location.Rack = dto.Rack.Trim(); location.Bin = dto.Bin.Trim(); location.IsActive = dto.IsActive;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(new { message = "Location updated. Historical postings remain unchanged." });
    }
}
public class StoreDto
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
}
public class LocationDto
{
    [Range(1, int.MaxValue)] public int StoreId { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(60)] public string Rack { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(60)] public string Bin { get; set; } = "";
}
public class EditLocationDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(60)] public string Rack { get; set; } = "";
    [Required(AllowEmptyStrings = true), StringLength(60)] public string Bin { get; set; } = "";
    public bool IsActive { get; set; }
}

public class EditStoreDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}
