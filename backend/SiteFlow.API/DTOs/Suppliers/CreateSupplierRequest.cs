using System.ComponentModel.DataAnnotations;

namespace SiteFlow.API.DTOs.Suppliers;

public class CreateSupplierRequest
{
    [Required]
    [StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(254)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}