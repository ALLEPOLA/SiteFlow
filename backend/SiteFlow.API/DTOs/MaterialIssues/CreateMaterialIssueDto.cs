using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.MaterialIssues;

public class CreateMaterialIssueDto
{
    [Required]
    [StringLength(36)]
    public string ClientRequestId { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }

    public DateOnly IssueDate { get; set; }

    [Required]
    [StringLength(1000)]
    public string Purpose { get; set; } = "";

    [Required]
    [StringLength(150)]
    public string ReceivedBy { get; set; } = "";

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreateMaterialIssueLineDto> Lines { get; set; } = new();
}

public class CreateMaterialIssueLineDto
{
    [Range(1, int.MaxValue)]
    public int? ReservationId { get; set; }

    [Range(1, int.MaxValue)]
    public int MaterialId { get; set; }

    [Range(
        typeof(decimal),
        "0.001",
        "999999999.999")]
    public decimal Quantity { get; set; }
}