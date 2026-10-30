using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.Materials;

public class CreateMaterialRequest
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Unit { get; set; } = string.Empty;
}