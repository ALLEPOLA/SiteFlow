using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.MaterialRequests;
using SiteFlow.API.Models;

namespace SiteFlow.API.Controllers;

[ApiController]
[Route("api/material-requests")]
public class MaterialRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MaterialRequestsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MaterialRequestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var requests = await _context.MaterialRequests
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Material)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialRequestResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var request = await _context.MaterialRequests
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Material)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request is null)
        {
            return NotFound(new
            {
                message = "Material request not found."
            });
        }

        return Ok(ToResponse(request));
    }

    [HttpPost]
    public async Task<ActionResult<MaterialRequestResponse>> Create(
        CreateMaterialRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.RequiredDate == default)
        {
            return BadRequest(new
            {
                message = "A required date is needed."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new
            {
                message = "A reason is required."
            });
        }

        if (dto.Lines is null || dto.Lines.Count == 0)
        {
            return BadRequest(new
            {
                message = "Add at least one material."
            });
        }

        var materialIds = dto.Lines
            .Select(l => l.MaterialId)
            .ToList();

        if (materialIds.Distinct().Count() != materialIds.Count)
        {
            return BadRequest(new
            {
                message = "Each material can appear only once per request."
            });
        }

        foreach (var line in dto.Lines)
        {
            if (decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return BadRequest(new
                {
                    message = "Quantities can have at most 3 decimal places."
                });
            }

            if (decimal.Round(line.EstimatedUnitPrice, 2)
                != line.EstimatedUnitPrice)
            {
                return BadRequest(new
                {
                    message = "Unit prices can have at most 2 decimal places."
                });
            }
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(
                p => p.Id == dto.ProjectId,
                cancellationToken);

        if (project is null)
        {
            return BadRequest(new
            {
                message = "The selected project does not exist."
            });
        }

        if (project.Status != "Active")
        {
            return BadRequest(new
            {
                message = "Select an active project."
            });
        }

        var materials = await _context.Materials
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        if (materials.Count != materialIds.Count)
        {
            return BadRequest(new
            {
                message = "One or more selected materials do not exist."
            });
        }

        if (materials.Values.Any(m => !m.IsActive))
        {
            return BadRequest(new
            {
                message = "Only active materials can be requested."
            });
        }

        var request = new MaterialRequest
        {
            ProjectId = project.Id,
            Project = project,
            RequiredDate = dto.RequiredDate,
            Reason = dto.Reason.Trim(),
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow,
            Lines = dto.Lines.Select(line => new MaterialRequestLine
            {
                MaterialId = line.MaterialId,
                Material = materials[line.MaterialId],
                Quantity = line.Quantity,
                EstimatedUnitPrice = line.EstimatedUnitPrice
            }).ToList()
        };

        _context.MaterialRequests.Add(request);

        // One SaveChanges call saves the header and lines together.
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = request.Id },
            ToResponse(request));
    }

    private static MaterialRequestResponse ToResponse(
        MaterialRequest request)
    {
        var lines = request.Lines
            .OrderBy(l => l.Id)
            .Select(line => new MaterialRequestLineResponse
            {
                Id = line.Id,
                MaterialId = line.MaterialId,
                MaterialCode = line.Material.Code,
                MaterialName = line.Material.Name,
                Unit = line.Material.Unit,
                Quantity = line.Quantity,
                EstimatedUnitPrice = line.EstimatedUnitPrice,
                LineTotal = decimal.Round(
                    line.Quantity * line.EstimatedUnitPrice,
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        return new MaterialRequestResponse
        {
            Id = request.Id,
            RequestNumber = $"MR-{request.Id:D6}",
            ProjectId = request.ProjectId,
            ProjectName = request.Project.Name,
            RequiredDate = request.RequiredDate,
            Reason = request.Reason,
            Status = request.Status,
            CreatedAtUtc = request.CreatedAtUtc,
            TotalEstimatedCost = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}