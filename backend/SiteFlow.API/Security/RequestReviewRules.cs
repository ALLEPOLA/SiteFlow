using System.Security.Claims;

namespace SiteFlow.API.Security;

public static class RequestReviewRules
{
    public static bool CanReview(
        ClaimsPrincipal user,
        string? creatorId,
        string status)
    {
        var userId = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return user.Identity?.IsAuthenticated == true
            && !string.IsNullOrWhiteSpace(userId)
            && !string.IsNullOrWhiteSpace(creatorId)
            && creatorId != userId
            && status == "Submitted"
            && (
                user.IsInRole(AppRoles.Administrator)
                || user.IsInRole(AppRoles.ProjectManager)
            );
    }
}