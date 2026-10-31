namespace SiteFlow.API.Security;

// Marks endpoints that remain available while a user
// is required to change their temporary password.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowPasswordChangeRequiredAttribute : Attribute
{
}