using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Airport.Api.Models;

public class Ticket
{
    public int Id { get; set; }

    public int PassengerId { get; set; }

    public int FlightId { get; set; }

    [Required]
    [MaxLength(9)]
    public string PassengerNationalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(254)]
    public string PassengerEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string SeatNumber { get; set; } = string.Empty;

    public int BaggageAllowanceKg { get; set; }

    public TicketClass Class { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    public TicketStatus Status { get; set; }

    public DateTime BookingDate { get; set; }

    [MaxLength(40)]
    public string? RouteBookingReference { get; set; }

    public int? RouteLegIndex { get; set; }

    public int? RouteLegCount { get; set; }

    [MaxLength(500)]
    public string? TicketFilePath { get; set; }

    [MaxLength(80)]
    public string? ActiveSeatKey { get; private set; }

    public User Passenger { get; set; } = null!;

    public Flight Flight { get; set; } = null!;

    public Baggage? Baggage { get; set; }
}
