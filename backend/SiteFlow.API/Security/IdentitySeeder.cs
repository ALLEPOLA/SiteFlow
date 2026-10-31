using Microsoft.AspNetCore.Identity;
using SiteFlow.API.Models;

namespace SiteFlow.API.Security;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();

        string[] roles =
        {
            AppRoles.Administrator,
            AppRoles.SiteEngineer,
            AppRoles.ProjectManager,
            AppRoles.ProcurementOfficer,
              AppRoles.Storekeeper
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(
                    await roleManager.CreateAsync(
                        new IdentityRole(role)));
            }
        }

        var email = configuration["InitialAdmin:Email"]?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "Set InitialAdmin:Email using User Secrets.");
        }

        // Existing accounts are not modified or promoted automatically.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var password = configuration["InitialAdmin:Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set InitialAdmin:Password using User Secrets.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "SiteFlow Administrator"
        };

        EnsureSucceeded(
            await userManager.CreateAsync(user, password));

        var roleResult = await userManager.AddToRoleAsync(
            user,
            AppRoles.Administrator);

        if (!roleResult.Succeeded)
        {
            // Allow the setup to be retried if role assignment fails.
            EnsureSucceeded(await userManager.DeleteAsync(user));
            EnsureSucceeded(roleResult);
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));
        }
    }
}