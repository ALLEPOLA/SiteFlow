using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SiteFlow.API.Models;

namespace SiteFlow.API.Security;

public class RequiredPasswordChangeFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _users;

    public RequiredPasswordChangeFilter(
        UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var account = await _users.GetUserAsync(context.HttpContext.User);
        if (account is null || !account.IsActive)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var allowed = context.HttpContext.GetEndpoint()?
            .Metadata
            .GetMetadata<AllowPasswordChangeRequiredAttribute>();

        if (allowed is not null)
        {
            await next();
            return;
        }

        var user = await _users.GetUserAsync(
            context.HttpContext.User);

        if (user is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (user.MustChangePassword)
        {
            context.Result = new ObjectResult(new
            {
                code = "PasswordChangeRequired",
                message =
                    "Change your temporary password before accessing SiteFlow."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

            return;
        }

        await next();
    }
}
