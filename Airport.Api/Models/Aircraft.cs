using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class Aircraft
{
    [Key]
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Manufacturer { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Model { get; set; } = string.Empty;

    public int SeatCount { get; set; }

    public int ManufactureYear { get; set; }
}
