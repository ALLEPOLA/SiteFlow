using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SiteFlow.API.Models;
using SiteFlow.API.Security;
using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Administrator)]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly AppDbContext _db;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        AppDbContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        var result = new List<object>();
        foreach (var user in users)
            result.Add(new { user.Id, user.FullName, user.Email, user.IsActive, user.MustChangePassword, roles = await _users.GetRolesAsync(user) });
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateStaffDto dto)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (id == _db.CurrentUserId || await _users.IsInRoleAsync(user, AppRoles.Administrator))
            return BadRequest(new { message = "Administrator accounts are managed outside staff administration. Use My Profile for your own name and password." });
        if (!new[] { AppRoles.SiteEngineer, AppRoles.ProjectManager, AppRoles.ProcurementOfficer, AppRoles.Storekeeper }.Contains(dto.Role) ||
            !await _roles.RoleExistsAsync(dto.Role) || string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Choose a staff role and enter a name and change reason." });
        await using var tx = await _db.Database.BeginTransactionAsync();
        user.FullName = dto.FullName.Trim();
        user.IsActive = dto.IsActive;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return IdentityFailure(updated);
        var removed = await _users.RemoveFromRolesAsync(user, await _users.GetRolesAsync(user));
        if (!removed.Succeeded) return IdentityFailure(removed);
        var added = await _users.AddToRoleAsync(user, dto.Role);
        if (!added.Succeeded) return IdentityFailure(added);
        var stamp = await _users.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return IdentityFailure(stamp);
        await tx.CommitAsync();
        return Ok(new { message = "Staff account updated. Previous sessions have ended." });
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, ResetStaffPasswordDto dto)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (id == _db.CurrentUserId || await _users.IsInRoleAsync(user, AppRoles.Administrator))
            return BadRequest(new { message = "Use My Profile to change an administrator password." });
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(new { message = "A reset reason is required." });
        await using var tx = await _db.Database.BeginTransactionAsync();
        user.MustChangePassword = true;
        var result = await _users.ResetPasswordAsync(user, await _users.GeneratePasswordResetTokenAsync(user), dto.Password);
        if (!result.Succeeded) return IdentityFailure(result);
        var unlocked = await _users.SetLockoutEndDateAsync(user, null);
        if (!unlocked.Succeeded) return IdentityFailure(unlocked);
        var failures = await _users.ResetAccessFailedCountAsync(user);
        if (!failures.Succeeded) return IdentityFailure(failures);
        await tx.CommitAsync();
        return Ok(new { message = "Temporary password reset. The staff member must change it after signing in." });
    }

    private BadRequestObjectResult IdentityFailure(IdentityResult result) =>
        BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

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

public class UpdateStaffDto
{
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required] public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public class ResetStaffPasswordDto
{
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
