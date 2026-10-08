using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record TicketDto(
    int Id,
    int PassengerId,
    string PassengerName,
    int FlightId,
    string FlightNumber,
    string SeatNumber,
    TicketClass Class,
    decimal Price,
    TicketStatus Status,
    string? TicketFilePath);

public record CreateTicketDto(
    int? PassengerId,
    int FlightId,
    [Required, MaxLength(10)] string SeatNumber,
    TicketClass Class,
    [Range(0, 999999)] decimal Price);

public record UpdateTicketOptionsDto(
    [Required, MaxLength(10)] string SeatNumber,
    TicketClass Class);
