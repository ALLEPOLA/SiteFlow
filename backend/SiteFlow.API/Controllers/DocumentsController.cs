using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/documents")]
public class DocumentsController(AppDbContext db) : ControllerBase
{
    private async Task<int?> Project(string type, int id) => type switch
    {
        "Project" => await db.Projects.Where(p => p.Id == id).Select(p => (int?)p.Id).SingleOrDefaultAsync(),
        "Request" => await db.MaterialRequests.Where(r => r.Id == id).Select(r => (int?)r.ProjectId).SingleOrDefaultAsync(),
        "Order" => await db.PurchaseOrders.Where(o => o.Id == id).Select(o => (int?)o.MaterialRequest.ProjectId).SingleOrDefaultAsync(),
        "Receipt" => await db.GoodsReceipts.Where(r => r.Id == id).Select(r => (int?)r.PurchaseOrder.MaterialRequest.ProjectId).SingleOrDefaultAsync(),
        "Issue" => await db.MaterialIssues.Where(i => i.Id == id).Select(i => (int?)i.ProjectId).SingleOrDefaultAsync(),
        "Invoice" when User.IsInRole("Administrator") || User.IsInRole("ProcurementOfficer") => await db.SupplierInvoices.Where(i => i.Id == id).Select(i => (int?)i.PurchaseOrder.MaterialRequest.ProjectId).SingleOrDefaultAsync(),
        _ => null
    };

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string resourceType, [FromQuery] int resourceId)
    {
        if (await Project(resourceType, resourceId) is null) return NotFound();
        return Ok(await db.DocumentAttachments.Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .Select(a => new { a.Id, a.FileName, a.CreatedAtUtc, size = a.Content.Length, a.UploadedByUserId }).OrderByDescending(a => a.Id).ToListAsync());
    }

    [HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] string resourceType, [FromForm] int resourceId, IFormFile file)
    {
        var projectId = await Project(resourceType, resourceId);
        if (projectId is null) return NotFound();
        if (file.Length is <= 0 or > 5 * 1024 * 1024) return BadRequest(new { message = "Choose a file between 1 byte and 5 MB." });
        var name = Path.GetFileName(file.FileName).Replace('\r', '_').Replace('\n', '_');
        if (name.Length > 200) return BadRequest(new { message = "File name must be at most 200 characters." });
        var type = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".txt" => "text/plain", _ => ""
        };
        if (type == "") return BadRequest(new { message = "Supported documents: PDF, PNG, JPG, and TXT." });
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var content = stream.ToArray();
        var valid = type switch
        {
            "application/pdf" => content.AsSpan().StartsWith("%PDF-"u8),
            "image/png" => content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => content.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
            _ => !content.Contains((byte)0)
        };
        if (!valid) return BadRequest(new { message = "The file content does not match its extension." });
        var attachment = new DocumentAttachment { ProjectId = projectId.Value, ResourceType = resourceType, ResourceId = resourceId,
            FileName = name, ContentType = type, Content = content, UploadedByUserId = db.CurrentUserId };
        db.DocumentAttachments.Add(attachment);
        await db.SaveChangesAsync();
        return Ok(new { attachment.Id, message = "Document attached." });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await db.DocumentAttachments.SingleOrDefaultAsync(a => a.Id == id);
        if (attachment is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "no-store";
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var attachment = await db.DocumentAttachments.SingleOrDefaultAsync(a => a.Id == id);
        if (attachment is null) return NotFound();
        if (!User.IsInRole("Administrator") && attachment.UploadedByUserId != db.CurrentUserId) return Forbid();
        db.DocumentAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        return Ok(new { message = "Attachment removed." });
    }
}
