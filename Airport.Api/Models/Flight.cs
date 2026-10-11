using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class Flight
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Origin { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    public string OriginAirportCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    public string DestinationAirportCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string BoardingGate { get; set; } = string.Empty;

    [MaxLength(40)]
    public string AircraftCode { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }

    public DateTime ArrivalTime { get; set; }

    public FlightStatus Status { get; set; }

    public int PassengerCount { get; set; }

    public decimal BasePrice { get; set; } = 180m;

    public int? GateId { get; set; }

    [MaxLength(120)]
    public string? ExternalId { get; set; }

    [Required]
    [MaxLength(60)]
    public string Source { get; set; } = "DemoSeed";

    public DateTime LastUpdatedAt { get; set; }

    public Gate? Gate { get; set; }

    public GeneratedFlightSchedule? GeneratedSchedule { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
