using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.MaterialRequests;

public class ReviewMaterialRequestDto
{
    [StringLength(1000)]
    public string? Comment { get; set; }
}