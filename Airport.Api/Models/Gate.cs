using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class Gate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string GateNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Terminal { get; set; } = string.Empty;

    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
}
