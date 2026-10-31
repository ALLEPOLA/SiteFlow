using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
using SiteFlow.API.Security;

namespace SiteFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
[AllowPasswordChangeRequired]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public ProfileController(
        AppDbContext context,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        _context = context;
        _users = users;
        _signIn = signIn;
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile(
        UpdateProfileDto dto)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        // Only the name is editable here.
        user.FullName = dto.FullName.Trim();

        var result = await _users.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return IdentityError(result);
        }

        await _signIn.RefreshSignInAsync(user);

        var roles = await _users.GetRolesAsync(user);

        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            roles,
            user.MustChangePassword
        });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordDto dto)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (await _users.IsLockedOutAsync(user))
        {
            await _signIn.SignOutAsync();

            return Unauthorized(new
            {
                message =
                    "The account is temporarily locked. Try signing in later."
            });
        }

        var validCurrentPassword = await _users.CheckPasswordAsync(
            user,
            dto.CurrentPassword);

        if (!validCurrentPassword)
        {
            var failed = await _users.AccessFailedAsync(user);

            if (!failed.Succeeded)
            {
                return IdentityError(failed);
            }

            if (await _users.IsLockedOutAsync(user))
            {
                await _signIn.SignOutAsync();

                return Unauthorized(new
                {
                    message =
                        "Too many incorrect attempts. Try signing in later."
                });
            }

            return BadRequest(new
            {
                message = "The current password is incorrect."
            });
        }

        if (dto.CurrentPassword == dto.NewPassword)
        {
            return BadRequest(new
            {
                message =
                    "Choose a password different from your current password."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        // These changes are saved together with the password.
        user.MustChangePassword = false;

        var changed = await _users.ChangePasswordAsync(
            user,
            dto.CurrentPassword,
            dto.NewPassword);

        if (!changed.Succeeded)
        {
            return IdentityError(changed);
        }

        var resetFailures = await _users.ResetAccessFailedCountAsync(
            user);

        if (!resetFailures.Succeeded)
        {
            return IdentityError(resetFailures);
        }

        // Explicitly invalidate all previously issued login cookies.
        var stampUpdated = await _users.UpdateSecurityStampAsync(user);

        if (!stampUpdated.Succeeded)
        {
            return IdentityError(stampUpdated);
        }

        await transaction.CommitAsync();

        await _signIn.SignOutAsync();

        return Ok(new
        {
            message =
                "Password changed successfully. Sign in with your new password."
        });
    }

    private BadRequestObjectResult IdentityError(
        IdentityResult result)
    {
        return BadRequest(new
        {
            message = string.Join(
                " ",
                result.Errors.Select(e => e.Description))
        });
    }
}

public class UpdateProfileDto
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = "";
}

public class ChangePasswordDto
{
    [Required]
    [StringLength(1024)]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; set; } = "";

    [Required]
    [Compare(
        nameof(NewPassword),
        ErrorMessage = "The new passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}