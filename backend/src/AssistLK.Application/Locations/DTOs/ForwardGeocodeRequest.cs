using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.Locations.DTOs;

public class ForwardGeocodeRequest
{
    [Required]
    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;
}
