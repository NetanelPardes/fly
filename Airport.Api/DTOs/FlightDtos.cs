using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record FlightDto(
    int Id,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    int? GateId,
    string? GateNumber,
    string? ExternalId,
    string Source,
    DateTime LastUpdatedAt);

public record CreateFlightDto(
    [Required, MaxLength(20)] string FlightNumber,
    [Required, MaxLength(80)] string Origin,
    [Required, MaxLength(80)] string Destination,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    int? GateId,
    [MaxLength(120)] string? ExternalId,
    [Required, MaxLength(60)] string Source);

public record UpdateFlightDto(
    [Required, MaxLength(20)] string FlightNumber,
    [Required, MaxLength(80)] string Origin,
    [Required, MaxLength(80)] string Destination,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    int? GateId,
    [MaxLength(120)] string? ExternalId,
    [Required, MaxLength(60)] string Source);
