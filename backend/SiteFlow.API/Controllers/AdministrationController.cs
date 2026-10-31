using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize(Roles = AppRoles.Administrator), Route("api/administration")]
public class AdministrationController(AppDbContext db) : ControllerBase
{
    [HttpGet("assignments")]
    public async Task<IActionResult> Assignments() => Ok(await db.ProjectAssignments.AsNoTracking()
        .Select(a => new { a.ProjectId, a.UserId, projectName = a.Project.Name, fullName = a.User.FullName })
        .OrderBy(a => a.ProjectId).ThenBy(a => a.fullName).ToListAsync());

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign(AssignmentDto dto)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == dto.ProjectId) ||
            !await db.Users.AnyAsync(u => u.Id == dto.UserId && u.IsActive))
            return BadRequest(new { message = "Select an existing project and active staff account." });
        // A project lock serializes assignment changes and prevents duplicate writes.
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Projects.FromSqlInterpolated($"SELECT * FROM `Projects` WHERE `Id` = {dto.ProjectId} FOR UPDATE").ToListAsync();
        if (!await db.ProjectAssignments.AnyAsync(a => a.ProjectId == dto.ProjectId && a.UserId == dto.UserId))
        {
            db.ProjectAssignments.Add(new ProjectAssignment { ProjectId = dto.ProjectId, UserId = dto.UserId });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Ok(new { message = "Staff assigned to project." });
    }

    [HttpPost("assignments/remove")]
    public async Task<IActionResult> Unassign(AssignmentDto dto)
    {
        var assignment = await db.ProjectAssignments.FindAsync(dto.ProjectId, dto.UserId);
        if (assignment is null) return NotFound();
        db.ProjectAssignments.Remove(assignment);
        await db.SaveChangesAsync();
        return Ok(new { message = "Project assignment removed." });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, [FromQuery] string? search = null)
    {
        if (page < 1 || page > 1000000) return BadRequest();
        var query = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Action.Contains(search) || a.ActorName.Contains(search) || a.Resource.Contains(search));
        return Ok(new { totalCount = await query.CountAsync(), items = await query.OrderByDescending(a => a.Id).Skip((page - 1) * 50).Take(50).ToListAsync() });
    }
}

public class AssignmentDto
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    [Required] public string UserId { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
