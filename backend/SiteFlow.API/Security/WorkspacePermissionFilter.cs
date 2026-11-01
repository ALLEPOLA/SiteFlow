using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SiteFlow.API.Security;

// UI visibility is backed by the same role matrix on controller requests.
// Catalog lookups remain shared because permitted forms need them.
public class WorkspacePermissionFilter : IAsyncActionFilter
{
    private static readonly IReadOnlyDictionary<string, string> Features = new Dictionary<string, string>
    {
        ["MaterialReservations"] = "reservations",
        ["Dashboard"] = "dashboard", ["ProjectCosts"] = "costs", ["MaterialPlanning"] = "planning",
        ["MaterialRequests"] = "requests", ["PurchaseOrders"] = "orders",
        ["GoodsReceipts"] = "receipts", ["MaterialIssues"] = "issues", ["Inventory"] = "inventory",
        ["InventoryOperations"] = "stock-operations", ["SupplierAccounts"] = "accounts",
        ["Corrections"] = "corrections", ["Documents"] = "documents", ["Reports"] = "reports",
        ["Notifications"] = "notifications", ["Administration"] = "users", ["Users"] = "users",
        ["Database"] = "users", ["WeatherForecast"] = "users",
        ["Projects"] = "projects", ["Materials"] = "materials", ["Suppliers"] = "materials"
    };
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        if (user.Identity?.IsAuthenticated == true &&
            Features.TryGetValue(controller, out var page) && !WorkspacePermissions.CanAccess(user, page))
        {
            context.Result = new ObjectResult(new { message = "Your role does not have access to this workspace." }) { StatusCode = 403 };
            return;
        }
        if (controller == "Reports" && context.ActionArguments.TryGetValue("kind", out var kind))
        {
            var feature = kind?.ToString() switch { "costs" => "costs", "orders" => "orders", "stock" => "inventory", _ => "reports" };
            if (!WorkspacePermissions.CanAccess(user, feature))
            {
                context.Result = new ObjectResult(new { message = "Your role cannot export this report type." }) { StatusCode = 403 };
                return;
            }
        }
        await next();
    }
}
