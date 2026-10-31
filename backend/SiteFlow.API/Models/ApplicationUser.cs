using Microsoft.AspNetCore.Identity;

namespace SiteFlow.API.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
}
