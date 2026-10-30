namespace SiteFlow.API.Models;

public class Project
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public decimal Budget { get; set; }

    public string Status { get; set; } = "Active";
}