namespace SiteFlow.API.Models;

public class Store
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<StorageLocation> Locations { get; set; } = new();
}
public class StorageLocation
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rack { get; set; } = "";
    public string Bin { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
