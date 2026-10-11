using System.ComponentModel.DataAnnotations;

namespace AssistLK.Application.Locations.DTOs;

public class ReverseGeocodeRequest
{
    [Required, Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Required, Range(-180, 180)]
    public decimal? Longitude { get; set; }
}
