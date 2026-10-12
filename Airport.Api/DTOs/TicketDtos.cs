using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record TicketDto(
    int Id,
    int PassengerId,
    string PassengerName,
    string PassengerNationalId,
    string PassengerEmail,
    int FlightId,
    string FlightNumber,
    string? AirlineName,
    string SeatNumber,
    int BaggageAllowanceKg,
    TicketClass Class,
    decimal Price,
    TicketStatus Status,
    string? TicketFilePath,
    DateTime BookingDate,
    string Origin,
    string OriginAirportCode,
    string? OriginAirportName,
    string? OriginCountry,
    string Destination,
    string DestinationAirportCode,
    string? DestinationAirportName,
    string? DestinationCountry,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    string BoardingGate,
    string AircraftCode,
    string? AircraftManufacturer,
    string? AircraftModel,
    int? SeatCapacity,
    FlightStatus FlightStatus,
    decimal FlightBasePrice);

public record CreateTicketDto(
    int? PassengerId,
    int FlightId,
    [MaxLength(9)] string? PassengerNationalId,
    [EmailAddress, MaxLength(254)] string? PassengerEmail,
    [MaxLength(10)] string? SeatNumber,
    int? BaggageAllowanceKg,
    TicketClass Class,
    [Range(0, 999999)] decimal? Price);

public record UpdateTicketOptionsDto(
    [Required, MaxLength(10)] string SeatNumber,
    TicketClass Class,
    int? BaggageAllowanceKg);
