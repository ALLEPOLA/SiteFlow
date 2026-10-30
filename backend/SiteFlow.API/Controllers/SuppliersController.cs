using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.Suppliers;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuppliersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Supplier>>> GetSuppliers(
        CancellationToken cancellationToken)
    {
        var suppliers = await _context.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        return Ok(suppliers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Supplier>> GetSupplier(
        int id,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == id,
                cancellationToken);

        if (supplier is null)
        {
            return NotFound();
        }

        return Ok(supplier);
    }

    [HttpPost]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProcurementOfficer)]
    public async Task<ActionResult<Supplier>> CreateSupplier(
        CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Supplier code and name are required."
            });
        }

        if (code.Length > 30)
        {
            return BadRequest(new
            {
                message = "Supplier code must not exceed 30 characters."
            });
        }

        var codeExists = await _context.Suppliers
            .AnyAsync(s => s.Code == code, cancellationToken);

        if (codeExists)
        {
            return Conflict(new
            {
                message = "A supplier with this code already exists."
            });
        }

        var supplier = new Supplier
        {
            Code = code,
            Name = name,
            ContactPerson = request.ContactPerson?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty,
            IsActive = true
        };

        _context.Suppliers.Add(supplier);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict(new
            {
                message = "A supplier with this code already exists."
            });
        }

        return CreatedAtAction(
            nameof(GetSupplier),
            new { id = supplier.Id },
            supplier);
    }
}