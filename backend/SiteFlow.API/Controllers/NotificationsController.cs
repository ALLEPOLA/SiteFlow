using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Services;

namespace SiteFlow.API.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public class NotificationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rows = new List<(string Key, string Message, string Page)>();
        if (User.IsInRole("Administrator") || User.IsInRole("ProjectManager"))
        {
            var requests = await db.MaterialRequests.Where(r => r.Status == "Submitted" && r.CreatedByUserId != null && r.CreatedByUserId != db.CurrentUserId)
                .OrderBy(r => r.SubmittedAtUtc).Take(100).Select(r => new { r.Id, r.Project.Name }).ToListAsync();
            rows.AddRange(requests.Select(r => ($"request-{r.Id}", $"MR-{r.Id:D6} for {r.Name} needs review.", "requests")));
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var orders = await db.PurchaseOrders.Where(o => (o.Status == "Issued" || o.Status == "PartiallyReceived") && o.ExpectedDeliveryDate < today)
            .OrderBy(o => o.ExpectedDeliveryDate).Take(100).Select(o => new { o.Id, o.ExpectedDeliveryDate }).ToListAsync();
        rows.AddRange(orders.Select(o => ($"order-{o.Id}", $"PO-{o.Id:D6} delivery was due {o.ExpectedDeliveryDate:yyyy-MM-dd}.", "orders")));
        var stocks = await db.StockMovements.GroupBy(m => new { m.ProjectId, m.MaterialId, m.Material.Name, m.Material.ReorderLevel })
            .Select(g => new { g.Key.ProjectId, g.Key.MaterialId, g.Key.Name, g.Key.ReorderLevel, Quantity = g.Sum(m => m.Quantity) })
            .Where(s => s.ReorderLevel > 0 && s.Quantity <= s.ReorderLevel).Take(100).ToListAsync();
        rows.AddRange(stocks.Select(s => ($"stock-{s.ProjectId}-{s.MaterialId}", $"{s.Name} stock in project {s.ProjectId} is {s.Quantity}; reorder level {s.ReorderLevel}.", "inventory")));
        if (User.IsInRole("Administrator") || User.IsInRole("ProcurementOfficer"))
        {
            var invoices = await db.SupplierInvoices.Include(i => i.Lines).Include(i => i.Payments).Where(i => i.Status == "Posted" && i.DueDate < today).ToListAsync();
            rows.AddRange(invoices.Where(i => i.Lines.Sum(l => OperationRules.Money(l.Quantity, l.UnitPrice)) > i.Payments.Sum(p => p.Amount))
                .Select(i => ($"invoice-{i.Id}", $"Supplier invoice {i.Number} is overdue.", "accounts")));
        }
        var read = await db.NotificationReads.Where(n => n.UserId == db.CurrentUserId).Select(n => n.Key).ToListAsync();
        return Ok(rows.Select(r => new { key = r.Key, message = r.Message, page = r.Page, isRead = read.Contains(r.Key) }));
    }

    [HttpPost("read")]
    public async Task<IActionResult> Read(ReadNotificationDto dto)
    {
        if (!await db.NotificationReads.AnyAsync(n => n.UserId == db.CurrentUserId && n.Key == dto.Key))
        {
            db.NotificationReads.Add(new NotificationRead { UserId = db.CurrentUserId, Key = dto.Key });
            await db.SaveChangesAsync();
        }
        return Ok(new { message = "Notification marked read." });
    }

    [HttpPut("reorder-level/{id:int}"), Authorize(Roles = "Administrator,ProcurementOfficer")]
    public async Task<IActionResult> ReorderLevel(int id, ReorderDto dto)
    {
        if (dto.Quantity < 0 || !OperationRules.Quantity(dto.Quantity)) return BadRequest();
        var material = await db.Materials.FindAsync(id);
        if (material is null) return NotFound();
        material.ReorderLevel = dto.Quantity;
        await db.SaveChangesAsync();
        return Ok(new { message = "Reorder level updated." });
    }
}
public class ReadNotificationDto
{
    [Required, StringLength(100)] public string Key { get; set; } = "";
}
public class ReorderDto { public decimal Quantity { get; set; } }
