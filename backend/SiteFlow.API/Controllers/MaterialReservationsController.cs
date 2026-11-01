using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/material-reservations")]
public class MaterialReservationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? projectId, [FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        Response.Headers["Cache-Control"] = "no-store";
        if (projectId is <= 0) return BadRequest();
        var canManage = User.IsInRole("Administrator") || User.IsInRole("ProjectManager");
        var mayRelease = canManage || User.IsInRole("SiteEngineer");
        var query = db.MaterialReservations.AsNoTracking().Where(r => !projectId.HasValue || r.ProjectId == projectId);
        if (activeOnly) query = query.Where(r => r.Quantity > r.ConsumedQuantity + r.ReleasedQuantity);
        return Ok(await query.OrderByDescending(r => r.Id).Take(500).Select(r => new
        {
            r.Id, r.ProjectId, projectName = r.Project.Name, r.MaterialRequestId,
            requestNumber = "MR-" + r.MaterialRequestId.ToString().PadLeft(6, '0'),
            r.MaterialId, materialCode = r.Material.Code, materialName = r.Material.Name, r.Material.Unit,
            r.Quantity, r.ConsumedQuantity, r.ReleasedQuantity,
            remainingQuantity = r.Quantity - r.ConsumedQuantity - r.ReleasedQuantity,
            r.Reason, r.ReleaseReason, r.CreatedByUserId, createdByName = r.CreatedByUser.FullName,
            r.CreatedAtUtc, r.ReleasedAtUtc,
            canRelease = mayRelease && r.Quantity > r.ConsumedQuantity + r.ReleasedQuantity &&
                (r.CreatedByUserId == db.CurrentUserId || canManage)
        }).ToListAsync(ct));
    }

    [HttpGet("eligible-requests"), Authorize(Roles = "Administrator,SiteEngineer,ProjectManager")]
    public async Task<IActionResult> Eligible(CancellationToken ct)
    {
        Response.Headers["Cache-Control"] = "no-store";
        var manager = User.IsInRole("Administrator") || User.IsInRole("ProjectManager");
        var lines = await db.MaterialRequestLines.AsNoTracking()
            .Where(l => l.MaterialRequest.Status == "Approved" && l.MaterialRequest.Project.Status == "Active" &&
                l.Material.IsActive && (manager || l.MaterialRequest.CreatedByUserId == db.CurrentUserId))
            .Select(l => new { l.MaterialRequestId, l.MaterialRequest.ProjectId,
                projectName = l.MaterialRequest.Project.Name, l.MaterialRequest.RequiredDate,
                l.MaterialId, materialCode = l.Material.Code, materialName = l.Material.Name, l.Material.Unit,
                approvedQuantity = l.Quantity,
                allocatedQuantity = db.MaterialReservations.Where(r => r.MaterialRequestId == l.MaterialRequestId && r.MaterialId == l.MaterialId)
                    .Sum(r => (decimal?)(r.Quantity - r.ReleasedQuantity)) ?? 0m })
            .ToListAsync(ct);
        return Ok(lines.Where(l => l.approvedQuantity > l.allocatedQuantity));
    }

    [HttpPost, Authorize(Roles = "Administrator,SiteEngineer,ProjectManager")]
    public async Task<IActionResult> Create(ReserveMaterialDto dto, CancellationToken ct)
    {
        if (!Guid.TryParse(dto.ClientRequestId, out var key) || key == Guid.Empty ||
            dto.Quantity <= 0 || !OperationRules.Quantity(dto.Quantity) || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Enter a valid submission ID, positive quantity (up to 3 decimals), and reason." });
        var requestKey = key.ToString("D");
        var info = await db.MaterialRequests.AsNoTracking().Where(r => r.Id == dto.MaterialRequestId)
            .Select(r => new { r.ProjectId }).SingleOrDefaultAsync(ct);
        if (info is null) return NotFound(new { message = "Request not found or not assigned to you." });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await OperationRules.LockProject(db, info.ProjectId))
            return BadRequest(new { message = "Project must be active and assigned to you." });
        var replay = await db.MaterialReservations.SingleOrDefaultAsync(r => r.ClientRequestId == requestKey, ct);
        if (replay is not null)
        {
            if (replay.CreatedByUserId != db.CurrentUserId || replay.MaterialRequestId != dto.MaterialRequestId ||
                replay.MaterialId != dto.MaterialId || replay.Quantity != dto.Quantity || replay.Reason != dto.Reason.Trim())
                return Conflict(new { message = "Submission ID already used for different reservation data." });
            return Ok(new { replay.Id, message = "Reservation already recorded." });
        }
        var request = await db.MaterialRequests.AsNoTracking().SingleAsync(r => r.Id == dto.MaterialRequestId, ct);
        if (!User.IsInRole("Administrator") && !User.IsInRole("ProjectManager") && request.CreatedByUserId != db.CurrentUserId)
            return Forbid();
        if (request.Status != "Approved") return Conflict(new { message = "Only approved requests can reserve stock." });
        var line = await db.MaterialRequestLines.AsNoTracking().SingleOrDefaultAsync(l =>
            l.MaterialRequestId == request.Id && l.MaterialId == dto.MaterialId && l.Material.IsActive, ct);
        if (line is null) return BadRequest(new { message = "Select an active material on this approved request." });
        var allocated = await db.MaterialReservations.Where(r => r.MaterialRequestId == request.Id && r.MaterialId == dto.MaterialId)
            .SumAsync(r => (decimal?)(r.Quantity - r.ReleasedQuantity), ct) ?? 0;
        if (dto.Quantity > line.Quantity - allocated)
            return Conflict(new { message = "Quantity exceeds the approved amount not already reserved or consumed." });
        var stock = await OperationRules.Stock(db, info.ProjectId, dto.MaterialId);
        var held = await ReservationRules.Held(db, info.ProjectId, dto.MaterialId, ct);
        if (dto.Quantity > ReservationRules.Available(stock.Quantity, held))
            return Conflict(new { message = $"Only {ReservationRules.Available(stock.Quantity, held)} unreserved units are available. Receive stock or release another hold first." });
        var reservation = new MaterialReservation
        {
            ClientRequestId = requestKey, ProjectId = info.ProjectId, MaterialRequestId = request.Id,
            MaterialId = dto.MaterialId, Quantity = dto.Quantity, Reason = dto.Reason.Trim(),
            CreatedByUserId = db.CurrentUserId
        };
        db.MaterialReservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new { reservation.Id, message = "Material reserved for the approved request." });
    }

    [HttpPost("{id:int}/release"), Authorize(Roles = "Administrator,SiteEngineer,ProjectManager")]
    public async Task<IActionResult> Release(int id, ReasonDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest();
        var projectId = await db.MaterialReservations.AsNoTracking().Where(r => r.Id == id).Select(r => (int?)r.ProjectId).SingleOrDefaultAsync(ct);
        if (projectId is null) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Release is permitted even if a project is inactive, but always uses the same project lock.
        var project = await db.Projects.FromSqlInterpolated($"SELECT * FROM `Projects` WHERE `Id` = {projectId.Value} FOR UPDATE").ToListAsync(ct);
        if (project.Count == 0) return NotFound();
        var reservation = await db.MaterialReservations.SingleAsync(r => r.Id == id, ct);
        if (!User.IsInRole("Administrator") && !User.IsInRole("ProjectManager") && reservation.CreatedByUserId != db.CurrentUserId)
            return Forbid();
        var remaining = ReservationRules.Remaining(reservation.Quantity, reservation.ConsumedQuantity, reservation.ReleasedQuantity);
        if (remaining == 0) return Ok(new { reservation.Id, message = "No remaining quantity to release." });
        reservation.ReleasedQuantity += remaining;
        reservation.ReleaseReason = dto.Reason.Trim();
        reservation.ReleasedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { reservation.Id, message = "Remaining reservation released. Consumed quantities remain in history." });
    }
}
public class ReserveMaterialDto : ReasonDto
{
    [Required, StringLength(36)] public string ClientRequestId { get; set; } = "";
    [Range(1, int.MaxValue)] public int MaterialRequestId { get; set; }
    [Range(1, int.MaxValue)] public int MaterialId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999.999")] public decimal Quantity { get; set; }
}
