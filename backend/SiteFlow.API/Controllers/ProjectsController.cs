using SiteFlow.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.Projects;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProjectsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Project>>> GetProjects(
        CancellationToken cancellationToken)
    {
        var projects = await _context.Projects
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Project>> GetProject(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == id,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        return Ok(project);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<ActionResult<Project>> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var location = request.Location.Trim();

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(location))
        {
            return BadRequest(new
            {
                message = "Project code, name, and location are required."
            });
        }

        if (code.Length > 30)
        {
            return BadRequest(new
            {
                message = "Project code must not exceed 30 characters."
            });
        }

        if (decimal.Round(request.Budget, 2) != request.Budget)
        {
            return BadRequest(new
            {
                message = "Budget can have at most 2 decimal places."
            });
        }

        var codeExists = await _context.Projects
            .AnyAsync(p => p.Code == code, cancellationToken);

        if (codeExists)
        {
            return Conflict(new
            {
                message = "A project with this code already exists."
            });
        }

        var project = new Project
        {
            Code = code,
            Name = name,
            Location = location,
            Budget = request.Budget,
            Status = "Active"
        };

        _context.Projects.Add(project);
        _context.Stores.Add(StorageRules.DefaultStore(project));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict(new
            {
                message = "A project with this code already exists."
            });
        }

        return CreatedAtAction(
            nameof(GetProject),
            new { id = project.Id },
            project);
    }
}