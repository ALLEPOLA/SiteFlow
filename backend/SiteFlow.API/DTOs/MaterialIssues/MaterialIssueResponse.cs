namespace SiteFlow.API.DTOs.MaterialIssues;

public class MaterialIssueResponse
{
    public int Id { get; set; }

    public string IssueNumber { get; set; } = "";

    public string ClientRequestId { get; set; } = "";

    public int ProjectId { get; set; }

    public string ProjectCode { get; set; } = "";

    public string ProjectName { get; set; } = "";

    public DateOnly IssueDate { get; set; }

    public string Purpose { get; set; } = "";

    public string ReceivedBy { get; set; } = "";

    public string Notes { get; set; } = "";

    public string Status { get; set; } = "";

    public string CreatedByName { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }

    public decimal TotalValue { get; set; }

    public List<MaterialIssueLineResponse> Lines { get; set; } = new();
}

public class MaterialIssueLineResponse
{
    public int? ReservationId { get; set; }
    public int Id { get; set; }

    public int MaterialId { get; set; }

    public string MaterialCode { get; set; } = "";

    public string MaterialName { get; set; } = "";

    public string Unit { get; set; } = "";

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }
}