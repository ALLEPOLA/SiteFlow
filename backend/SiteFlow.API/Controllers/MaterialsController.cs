using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.Materials;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/materials")]
public class MaterialsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MaterialsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Material>>> GetMaterials(
        CancellationToken cancellationToken)
    {
        var materials = await _context.Materials
            .AsNoTracking()
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);

        return Ok(materials);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Material>> GetMaterial(
        int id,
        CancellationToken cancellationToken)
    {
        var material = await _context.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == id,
                cancellationToken);

        if (material is null)
        {
            return NotFound();
        }

        return Ok(material);
    }

    [HttpPost]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<ActionResult<Material>> CreateMaterial(
        CreateMaterialRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var category = request.Category.Trim();
        var unit = request.Unit.Trim();

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(unit))
        {
            return BadRequest(new
            {
                message = "All material fields are required."
            });
        }

        if (code.Length > 30)
        {
            return BadRequest(new
            {
                message = "Material code must not exceed 30 characters."
            });
        }

        var codeExists = await _context.Materials
            .AnyAsync(m => m.Code == code, cancellationToken);

        if (codeExists)
        {
            return Conflict(new
            {
                message = "A material with this code already exists."
            });
        }

        var material = new Material
        {
            Code = code,
            Name = name,
            Category = category,
            Unit = unit,
            IsActive = true
        };

        _context.Materials.Add(material);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict(new
            {
                message = "A material with this code already exists."
            });
        }

        return CreatedAtAction(
            nameof(GetMaterial),
            new { id = material.Id },
            material);
    }
}