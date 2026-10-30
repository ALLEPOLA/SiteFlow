namespace SiteFlow.API.DTOs.MaterialRequests;

public class MaterialRequestResponse
{
    public int Id { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public DateOnly RequiredDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public decimal TotalEstimatedCost { get; set; }

    public List<MaterialRequestLineResponse> Lines { get; set; } = new();
}

public class MaterialRequestLineResponse
{
    public int Id { get; set; }

    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal EstimatedUnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}