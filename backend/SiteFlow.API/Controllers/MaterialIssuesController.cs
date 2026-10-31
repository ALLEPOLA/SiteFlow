using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using SiteFlow.API.Data;
using SiteFlow.API.DTOs.MaterialIssues;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/material-issues")]
public class MaterialIssuesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MaterialIssuesController(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<MaterialIssue> IssueQuery()
    {
        return _context.MaterialIssues
            .AsNoTracking()
            .Include(i => i.Project)
            .Include(i => i.CreatedByUser)
            .Include(i => i.Lines)
                .ThenInclude(l => l.Material);
    }

    [HttpGet]
    public async Task<ActionResult<List<MaterialIssueResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var issues = await IssueQuery()
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken);

        return Ok(issues.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialIssueResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var issue = await IssueQuery()
            .FirstOrDefaultAsync(
                i => i.Id == id,
                cancellationToken);

        if (issue is null)
        {
            return NotFound(new
            {
                message = "Material issue not found."
            });
        }

        return Ok(ToResponse(issue));
    }

    [HttpPost]
    [Authorize(
        Roles = AppRoles.Administrator + "," + AppRoles.Storekeeper)]
    public async Task<ActionResult<MaterialIssueResponse>> Create(
        CreateMaterialIssueDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId) ||
            !await _context.Users.AnyAsync(
                u => u.Id == userId,
                cancellationToken))
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(dto.ClientRequestId, out var requestGuid) ||
            requestGuid == Guid.Empty)
        {
            return BadRequest(new
            {
                message =
                    "ClientRequestId must be a valid nonempty UUID."
            });
        }

        var requestKey = requestGuid.ToString("D");

        if (dto.ProjectId <= 0)
        {
            return BadRequest(new
            {
                message = "Select a valid project."
            });
        }

        if (dto.IssueDate == default)
        {
            return BadRequest(new
            {
                message = "Issue date is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Purpose) ||
            string.IsNullOrWhiteSpace(dto.ReceivedBy))
        {
            return BadRequest(new
            {
                message = "Purpose and received-by name are required."
            });
        }

        if (dto.Lines is null ||
            dto.Lines.Count == 0 ||
            dto.Lines.Count > 100 ||
            dto.Lines.Any(l => l is null))
        {
            return BadRequest(new
            {
                message = "Add between 1 and 100 valid issue lines."
            });
        }

        var materialIds = dto.Lines
            .Select(l => l.MaterialId)
            .ToList();

        if (materialIds.Any(id => id <= 0) ||
            materialIds.Distinct().Count() != materialIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "Each material must be valid and appear only once."
            });
        }

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0 ||
                line.Quantity > 999999999.999m ||
                decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                return BadRequest(new
                {
                    message =
                        "Quantities must be positive, within range, and have at most 3 decimal places."
                });
            }
        }

        var existing = await IssueQuery()
            .FirstOrDefaultAsync(
                i => i.ClientRequestId == requestKey,
                cancellationToken);

        if (existing is not null)
        {
            return ReplayResult(existing, dto, userId);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // All material issues for this project use the same lock.
        // A second issue waits until the first commits.
        var lockedProjects = await _context.Projects
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM `Projects`
                WHERE `Id` = {dto.ProjectId}
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var project = lockedProjects.SingleOrDefault();

        if (project is null)
        {
            return BadRequest(new
            {
                message = "Project not found."
            });
        }

        // Recheck after waiting for the project lock.
        existing = await IssueQuery()
            .FirstOrDefaultAsync(
                i => i.ClientRequestId == requestKey,
                cancellationToken);

        if (existing is not null)
        {
            return ReplayResult(existing, dto, userId);
        }

        if (project.Status != "Active")
        {
            return BadRequest(new
            {
                message = "Materials can only be issued to active projects."
            });
        }

        var materials = await _context.Materials
            .AsNoTracking()
            .Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(
                m => m.Id,
                cancellationToken);

        if (materialIds.Any(id =>
                !materials.ContainsKey(id) ||
                !materials[id].IsActive))
        {
            return BadRequest(new
            {
                message = "All selected materials must exist and be active."
            });
        }

        // Require old receipts to be imported before issuing their stock.
        var hasMissingReceiptStock =
            await _context.GoodsReceiptLines.AnyAsync(
                line =>
                    line.GoodsReceipt.Status == "Posted" &&
                    line.GoodsReceipt.PurchaseOrder
                        .MaterialRequest.ProjectId == dto.ProjectId &&
                    materialIds.Contains(
                        line.PurchaseOrderLine
                            .MaterialRequestLine.MaterialId) &&
                    !_context.StockMovements.Any(m =>
                        m.GoodsReceiptLineId == line.Id),
                cancellationToken);

        if (hasMissingReceiptStock)
        {
            return Conflict(new
            {
                message =
                    "Some posted receipts are missing stock movements. Ask an Administrator to import existing receipts, then retry."
            });
        }

        // One aggregate query supplies available quantity and receipt costs.
        var stock = await _context.StockMovements
            .AsNoTracking()
            .Where(m =>
                m.ProjectId == dto.ProjectId &&
                materialIds.Contains(m.MaterialId))
            .GroupBy(m => m.MaterialId)
            .Select(group => new
            {
                MaterialId = group.Key,

                QuantityOnHand = group.Sum(m => m.Quantity),

                ReceiptQuantity = group.Sum(m => m.Quantity),

                ReceiptValue = group.Sum(m => m.Quantity * m.UnitCost),

                LatestMovementDate = group.Max(m => m.MovementDate)
            })
            .ToDictionaryAsync(
                item => item.MaterialId,
                cancellationToken);

        foreach (var line in dto.Lines)
        {
            var material = materials[line.MaterialId];

            if (!stock.TryGetValue(line.MaterialId, out var available))
            {
                return Conflict(new
                {
                    message =
                        $"No stock is available for {material.Code}."
                });
            }

            if (line.Quantity > available.QuantityOnHand)
            {
                return Conflict(new
                {
                    message =
                        $"{material.Code} has only {available.QuantityOnHand} {material.Unit} available."
                });
            }

            // Keeps issue dates in order for each project's material.
            if (dto.IssueDate < available.LatestMovementDate)
            {
                return BadRequest(new
                {
                    message =
                        $"The issue date for {material.Code} cannot be before its latest stock movement on {available.LatestMovementDate:yyyy-MM-dd}."
                });
            }

            if (available.ReceiptQuantity <= 0)
            {
                return Conflict(new
                {
                    message =
                        $"Receipt cost information is unavailable for {material.Code}."
                });
            }
        }

        var createdAt = DateTime.UtcNow;

        var issue = new MaterialIssue
        {
            ClientRequestId = requestKey,
            ProjectId = dto.ProjectId,
            IssueDate = dto.IssueDate,
            Purpose = dto.Purpose.Trim(),
            ReceivedBy = dto.ReceivedBy.Trim(),
            Notes = dto.Notes?.Trim() ?? string.Empty,
            Status = "Posted",
            CreatedByUserId = userId,
            CreatedAtUtc = createdAt,

            Lines = dto.Lines.Select(line =>
            {
                var available = stock[line.MaterialId];

                var unitCost = decimal.Round(
                    Math.Max(0, available.ReceiptValue / available.ReceiptQuantity),
                    2,
                    MidpointRounding.AwayFromZero);

                return new MaterialIssueLine
                {
                    MaterialId = line.MaterialId,
                    Quantity = line.Quantity,
                    UnitCost = unitCost
                };
            }).ToList()
        };

        _context.MaterialIssues.Add(issue);

        foreach (var line in issue.Lines)
        {
            _context.StockMovements.Add(new StockMovement
            {
                ProjectId = issue.ProjectId,
                MaterialId = line.MaterialId,
                MovementType = "Issue",

                // Reduce stock.
                Quantity = -line.Quantity,

                UnitCost = line.UnitCost,
                MovementDate = issue.IssueDate,

                // EF saves the issue line first and fills its FK.
                MaterialIssueLine = line,

                CreatedByUserId = userId,
                CreatedAtUtc = createdAt
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transaction.RollbackAsync(cancellationToken);

            _context.ChangeTracker.Clear();

            var duplicate = await IssueQuery()
                .FirstOrDefaultAsync(
                    i => i.ClientRequestId == requestKey,
                    cancellationToken);

            if (duplicate is not null)
            {
                return ReplayResult(duplicate, dto, userId);
            }

            return Conflict(new
            {
                message =
                    "A duplicate issue submission was detected. Reload and try again."
            });
        }

        await transaction.CommitAsync(cancellationToken);

        var saved = await IssueQuery()
            .SingleAsync(
                i => i.Id == issue.Id,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = issue.Id },
            ToResponse(saved));
    }

    private ActionResult<MaterialIssueResponse> ReplayResult(
        MaterialIssue existing,
        CreateMaterialIssueDto dto,
        string userId)
    {
        var sameHeader =
            existing.CreatedByUserId == userId &&
            existing.ProjectId == dto.ProjectId &&
            existing.IssueDate == dto.IssueDate &&
            existing.Purpose == dto.Purpose.Trim() &&
            existing.ReceivedBy == dto.ReceivedBy.Trim() &&
            existing.Notes == (dto.Notes?.Trim() ?? string.Empty);

        var sameLines =
            existing.Lines.Count == dto.Lines.Count &&
            existing.Lines.All(saved =>
                dto.Lines.Any(incoming =>
                    incoming.MaterialId == saved.MaterialId &&
                    incoming.Quantity == saved.Quantity));

        if (!sameHeader || !sameLines)
        {
            return Conflict(new
            {
                message =
                    "This submission ID has already been used for different issue data."
            });
        }

        return Ok(ToResponse(existing));
    }

    private static MaterialIssueResponse ToResponse(
        MaterialIssue issue)
    {
        var lines = issue.Lines
            .OrderBy(l => l.Id)
            .Select(line => new MaterialIssueLineResponse
            {
                Id = line.Id,
                MaterialId = line.MaterialId,
                MaterialCode = line.Material.Code,
                MaterialName = line.Material.Name,
                Unit = line.Material.Unit,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,

                LineTotal = decimal.Round(
                    line.Quantity * line.UnitCost,
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        return new MaterialIssueResponse
        {
            Id = issue.Id,
            IssueNumber = $"MI-{issue.Id:D6}",
            ClientRequestId = issue.ClientRequestId,
            ProjectId = issue.ProjectId,
            ProjectCode = issue.Project.Code,
            ProjectName = issue.Project.Name,
            IssueDate = issue.IssueDate,
            Purpose = issue.Purpose,
            ReceivedBy = issue.ReceivedBy,
            Notes = issue.Notes,
            Status = issue.Status,
            CreatedByName = issue.CreatedByUser.FullName,
            CreatedAtUtc = issue.CreatedAtUtc,
            TotalValue = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}
