using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.MaterialRequests;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/material-requests")]
public class MaterialRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MaterialRequestsController(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<MaterialRequest> RequestQuery()
    {
        return _context.MaterialRequests
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Material);
    }

    [HttpGet]
    public async Task<ActionResult<List<MaterialRequestResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var requests = await RequestQuery()
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialRequestResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var request = await RequestQuery()
            .FirstOrDefaultAsync(
                r => r.Id == id,
                cancellationToken);

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
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.SiteEngineer)]
    public async Task<ActionResult<MaterialRequestResponse>> Create(
        CreateMaterialRequestDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var creator = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == userId,
                cancellationToken);

        if (creator is null)
        {
            return Unauthorized();
        }

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

        if (dto.Lines is null ||
            dto.Lines.Count == 0 ||
            dto.Lines.Count > 100)
        {
            return BadRequest(new
            {
                message = "Add between 1 and 100 material lines."
            });
        }

        var materialIds = dto.Lines
            .Select(l => l.MaterialId)
            .ToList();

        if (materialIds.Distinct().Count() != materialIds.Count)
        {
            return BadRequest(new
            {
                message = "Each material can appear only once."
            });
        }

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0 ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return BadRequest(new
                {
                    message =
                        "Quantities must be positive with at most 3 decimal places."
                });
            }

            if (line.EstimatedUnitPrice < 0 ||
                decimal.Round(line.EstimatedUnitPrice, 2)
                    != line.EstimatedUnitPrice)
            {
                return BadRequest(new
                {
                    message =
                        "Prices must be nonnegative with at most 2 decimal places."
                });
            }
        }

        var project = await _context.Projects
            .FirstOrDefaultAsync(
                p => p.Id == dto.ProjectId,
                cancellationToken);

        if (project is null || project.Status != "Active")
        {
            return BadRequest(new
            {
                message = "Select an existing active project."
            });
        }

        var materials = await _context.Materials
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(
                m => m.Id,
                cancellationToken);

        if (materials.Count != materialIds.Count ||
            materials.Values.Any(m => !m.IsActive))
        {
            return BadRequest(new
            {
                message = "Select existing active materials."
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
            CreatedByUserId = creator.Id,
            CreatedByUser = creator,

            Lines = dto.Lines.Select(line => new MaterialRequestLine
            {
                MaterialId = line.MaterialId,
                Material = materials[line.MaterialId],
                Quantity = line.Quantity,
                EstimatedUnitPrice = line.EstimatedUnitPrice
            }).ToList()
        };

        _context.MaterialRequests.Add(request);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = request.Id },
            ToResponse(request));
    }

    [HttpPost("{id:int}/submit")]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.SiteEngineer)]
    public async Task<ActionResult<MaterialRequestResponse>> Submit(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var request = await RequestQuery()
            .FirstOrDefaultAsync(
                r => r.Id == id,
                cancellationToken);

        if (request is null)
        {
            return NotFound(new
            {
                message = "Material request not found."
            });
        }

        if (request.CreatedByUserId != userId)
        {
            return StatusCode(403, new
            {
                message = "Only the creator can submit this request."
            });
        }

        if (request.Status != "Draft")
        {
            return Conflict(new
            {
                message = "Only draft requests can be submitted."
            });
        }

        if (request.Project.Status != "Active")
        {
            return BadRequest(new
            {
                message = "The project must be active."
            });
        }

        if (request.RequiredDate == default ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            request.Lines.Count == 0)
        {
            return BadRequest(new
            {
                message = "The request is incomplete."
            });
        }

        if (request.Lines.Any(line =>
            !line.Material.IsActive ||
            line.Quantity <= 0 ||
            line.EstimatedUnitPrice < 0))
        {
            return BadRequest(new
            {
                message = "Check the materials, quantities, and prices."
            });
        }

        var submittedAt = DateTime.UtcNow;

        var affectedRows = await _context.MaterialRequests
            .Where(r =>
                r.Id == id &&
                r.CreatedByUserId == userId &&
                r.Status == "Draft")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, "Submitted")
                    .SetProperty(
                        r => r.SubmittedAtUtc,
                        submittedAt),
                cancellationToken);

        if (affectedRows != 1)
        {
            return Conflict(new
            {
                message = "The request changed. Refresh and try again."
            });
        }

        return await GetById(id, cancellationToken);
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProjectManager)]
    public Task<ActionResult<MaterialRequestResponse>> Approve(
        int id,
        ReviewMaterialRequestDto dto,
        CancellationToken cancellationToken)
    {
        return Review(
            id,
            dto,
            "Approved",
            cancellationToken);
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.ProjectManager)]
    public Task<ActionResult<MaterialRequestResponse>> Reject(
        int id,
        ReviewMaterialRequestDto dto,
        CancellationToken cancellationToken)
    {
        return Review(
            id,
            dto,
            "Rejected",
            cancellationToken);
    }

    private async Task<ActionResult<MaterialRequestResponse>> Review(
        int id,
        ReviewMaterialRequestDto dto,
        string decision,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var request = await _context.MaterialRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == id,
                cancellationToken);

        if (request is null)
        {
            return NotFound(new
            {
                message = "Material request not found."
            });
        }

        if (request.CreatedByUserId == userId)
        {
            return StatusCode(403, new
            {
                message =
                    "You cannot approve or reject your own request."
            });
        }

        if (request.Status != "Submitted")
        {
            return Conflict(new
            {
                message =
                    "Only submitted requests can be reviewed."
            });
        }

        if (!RequestReviewRules.CanReview(
            User,
            request.CreatedByUserId,
            request.Status))
        {
            return StatusCode(403, new
            {
                message = "You cannot review this request."
            });
        }

        var comment = dto.Comment?.Trim();

        if (decision == "Rejected" &&
            string.IsNullOrWhiteSpace(comment))
        {
            return BadRequest(new
            {
                message = "A rejection reason is required."
            });
        }

        var reviewerExists = await _context.Users
            .AnyAsync(
                u => u.Id == userId,
                cancellationToken);

        if (!reviewerExists)
        {
            return Unauthorized();
        }

        var reviewedAt = DateTime.UtcNow;

        // The status condition prevents a second decision
        // from overwriting the first decision.
        var affectedRows = await _context.MaterialRequests
            .Where(r =>
                r.Id == id &&
                r.Status == "Submitted" &&
                r.CreatedByUserId != null &&
                r.CreatedByUserId != userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, decision)
                    .SetProperty(
                        r => r.ReviewedByUserId,
                        userId)
                    .SetProperty(
                        r => r.ReviewedAtUtc,
                        reviewedAt)
                    .SetProperty(
                        r => r.ReviewComment,
                        comment),
                cancellationToken);

        if (affectedRows != 1)
        {
            return Conflict(new
            {
                message =
                    "Another decision was recorded. Refresh this request."
            });
        }

        return await GetById(id, cancellationToken);
    }

    private MaterialRequestResponse ToResponse(
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

        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

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
            CreatedByUserId = request.CreatedByUserId,
            CreatedByName =
                request.CreatedByUser?.FullName ?? "Unknown",

            SubmittedAtUtc = request.SubmittedAtUtc,

            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedByName = request.ReviewedByUser?.FullName,
            ReviewedAtUtc = request.ReviewedAtUtc,
            ReviewComment = request.ReviewComment,

            CanSubmit =
                request.Status == "Draft" &&
                request.CreatedByUserId != null &&
                request.CreatedByUserId == userId &&
                (
                    User.IsInRole(AppRoles.Administrator) ||
                    User.IsInRole(AppRoles.SiteEngineer)
                ),

            CanReview = RequestReviewRules.CanReview(
                User,
                request.CreatedByUserId,
                request.Status),

            TotalEstimatedCost = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}