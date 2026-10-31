using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SiteFlow.API.Data;
using SiteFlow.API.Models;

namespace SiteFlow.API.Security;

// Record successful commands without storing request bodies or credentials.
public class OperationAuditFilter(AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        if (result.Exception is not null ||
            result.Result is ForbidResult or ChallengeResult ||
            context.HttpContext.Request.Method is "GET" or "HEAD" or "OPTIONS" ||
            context.RouteData.Values["controller"]?.ToString() == "Auth" ||
            (result.Result is ObjectResult obj && obj.StatusCode >= 400) ||
            (result.Result is StatusCodeResult status && status.StatusCode >= 400)) return;

        var reason = context.ActionArguments.Values.Select(v =>
            v?.GetType().GetProperty("Reason")?.GetValue(v)?.ToString())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
        var path = context.HttpContext.Request.Path.ToString();
        if (result.Result is ObjectResult success && success.Value is not null)
        {
            var value = success.Value;
            var id = value.GetType().GetProperty("Id")?.GetValue(value)
                ?? value.GetType().GetProperty("id")?.GetValue(value);
            if (id is not null) path += $" [record {id}]";
        }
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = db.CurrentUserId,
            ActorName = context.HttpContext.User.Identity?.Name ?? "",
            Action = $"{context.RouteData.Values["controller"]}.{context.RouteData.Values["action"]}",
            Resource = path[..Math.Min(300, path.Length)],
            Reason = reason[..Math.Min(1000, reason.Length)]
        });
        await db.SaveChangesAsync();
    }
}
