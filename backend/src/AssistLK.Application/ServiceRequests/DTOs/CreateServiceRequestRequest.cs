using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.ServiceRequests.DTOs;

public class CreateServiceRequestRequest
{
    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string LocationText { get; set; } = string.Empty;

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [MaxLength(100)]
    public string? CategoryHint { get; set; }
}
