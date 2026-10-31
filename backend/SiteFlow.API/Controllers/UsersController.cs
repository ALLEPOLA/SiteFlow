using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Administrator)]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        string[] allowedRoles =
        {
            AppRoles.SiteEngineer,
            AppRoles.ProjectManager,
            AppRoles.ProcurementOfficer,
            AppRoles.Storekeeper
        };

        if (!allowedRoles.Contains(dto.Role))
        {
            return BadRequest(new
            {
                message =
                    "Choose SiteEngineer, ProjectManager, ProcurementOfficer, or Storekeeper."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (!await _roles.RoleExistsAsync(dto.Role))
        {
            return BadRequest(new
            {
                message = "Role setup is incomplete."
            });
        }

        var email = dto.Email.Trim();

        if (await _users.FindByEmailAsync(email) is not null)
        {
            return Conflict(new
            {
                message = "An account already uses this email."
            });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = dto.FullName.Trim(),
            MustChangePassword = true
        };

        var created = await _users.CreateAsync(user, dto.Password);

        if (!created.Succeeded)
        {
            return BadRequest(new
            {
                message = string.Join(
                    " ",
                    created.Errors.Select(e => e.Description))
            });
        }

        var assigned = await _users.AddToRoleAsync(user, dto.Role);

        if (!assigned.Succeeded)
        {
            var cleanup = await _users.DeleteAsync(user);

            if (!cleanup.Succeeded)
            {
                throw new InvalidOperationException(
                    "Account setup failed. Contact the administrator.");
            }

            return BadRequest(new
            {
                message = "Role assignment failed. Please retry."
            });
        }

        return StatusCode(201, new
        {
            user.Id,
            user.FullName,
            user.Email,
            role = dto.Role,
            user.MustChangePassword
        });
    }
}

public class CreateUserDto
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";

    [Required]
    public string Role { get; set; } = "";
}