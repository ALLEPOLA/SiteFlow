using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.MaterialRequests;

public class CreateMaterialRequestDto
{
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }

    public DateOnly RequiredDate { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public List<CreateMaterialRequestLineDto> Lines { get; set; } = new();
}

public class CreateMaterialRequestLineDto
{
    [Range(1, int.MaxValue)]
    public int MaterialId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999.999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal EstimatedUnitPrice { get; set; }
}