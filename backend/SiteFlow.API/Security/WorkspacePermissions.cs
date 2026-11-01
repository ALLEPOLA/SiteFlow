using System.Security.Claims;
namespace SiteFlow.API.Security;

public static class WorkspacePermissions
{
    private static readonly IReadOnlyDictionary<string, string[]> RolePages = new Dictionary<string, string[]>
    {
        ["Administrator"] = new[] { "dashboard", "projects", "costs", "materials", "suppliers", "planning", "requests", "orders", "receipts", "inventory", "reservations", "issues", "corrections", "stock-operations", "accounts", "documents", "notifications", "reports", "audit", "users", "profile" },
        ["SiteEngineer"] = new[] { "dashboard", "projects", "materials", "planning", "requests", "inventory", "reservations", "corrections", "documents", "notifications", "reports", "profile" },
        ["ProjectManager"] = new[] { "dashboard", "projects", "costs", "materials", "suppliers", "planning", "requests", "orders", "receipts", "inventory", "reservations", "issues", "documents", "notifications", "reports", "profile" },
        ["ProcurementOfficer"] = new[] { "dashboard", "projects", "costs", "materials", "suppliers", "planning", "requests", "orders", "receipts", "inventory", "reservations", "corrections", "accounts", "documents", "notifications", "reports", "profile" },
        ["Storekeeper"] = new[] { "dashboard", "projects", "materials", "orders", "receipts", "inventory", "reservations", "issues", "corrections", "stock-operations", "documents", "notifications", "reports", "profile" },
    };
    public static string[] Pages(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? RolePages.Where(entry => user.IsInRole(entry.Key)).SelectMany(entry => entry.Value).Distinct().ToArray()
        : Array.Empty<string>();
    public static bool CanAccess(ClaimsPrincipal user, string page) => Pages(user).Contains(page);
    public static bool CanReview(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Administrator) || user.IsInRole(AppRoles.ProjectManager);
}
