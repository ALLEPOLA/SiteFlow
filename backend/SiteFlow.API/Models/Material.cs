namespace SiteFlow.API.Models;

public class Material
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}