namespace SiteFlow.API.Models;

public class MaterialRequestLine
{
    public int Id { get; set; }

    public int MaterialRequestId { get; set; }

    public MaterialRequest MaterialRequest { get; set; } = null!;

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal EstimatedUnitPrice { get; set; }
}