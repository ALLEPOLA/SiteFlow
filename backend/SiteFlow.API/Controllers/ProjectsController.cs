using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.Projects;
using SiteFlow.API.Models;

namespace SiteFlow.API.Controllers;

[ApiController]
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
    public async Task<ActionResult<Project>> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

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
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            Budget = request.Budget,
            Status = "Active"
        };

        _context.Projects.Add(project);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException
            {
                Number: 1062
            })
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