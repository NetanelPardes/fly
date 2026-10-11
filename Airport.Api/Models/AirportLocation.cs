using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class AirportLocation
{
    [Key]
    [MaxLength(12)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Country { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    public string Name { get; set; } = string.Empty;
}
