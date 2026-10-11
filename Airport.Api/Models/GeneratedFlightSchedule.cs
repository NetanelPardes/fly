using System.ComponentModel.DataAnnotations;

namespace Airport.Api.Models;

public class GeneratedFlightSchedule
{
    public int Id { get; set; }

    public int FlightId { get; set; }

    [Required]
    [MaxLength(40)]
    public string AircraftRegistration { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string AircraftManufacturer { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string AircraftModel { get; set; } = string.Empty;

    public int SeatCapacity { get; set; }

    public int ManufactureYear { get; set; }

    [Required]
    [MaxLength(40)]
    public string AirlineExternalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string AirlineName { get; set; } = string.Empty;

    [MaxLength(8)]
    public string? AirlineIata { get; set; }

    [MaxLength(8)]
    public string? AirlineIcao { get; set; }

    [MaxLength(80)]
    public string? AirlineCountry { get; set; }

    [Required]
    [MaxLength(40)]
    public string OriginAirportExternalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string OriginAirportName { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    public string OriginIataCode { get; set; } = string.Empty;

    [MaxLength(8)]
    public string? OriginIcaoCode { get; set; }

    [MaxLength(80)]
    public string? OriginCountry { get; set; }

    [Required]
    [MaxLength(40)]
    public string DestinationAirportExternalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string DestinationAirportName { get; set; } = string.Empty;

    [Required]
    [MaxLength(8)]
    public string DestinationIataCode { get; set; } = string.Empty;

    [MaxLength(8)]
    public string? DestinationIcaoCode { get; set; }

    [MaxLength(80)]
    public string? DestinationCountry { get; set; }

    public int DistanceKm { get; set; }

    public DateTime GeneratedAt { get; set; }

    public Flight Flight { get; set; } = null!;
}
