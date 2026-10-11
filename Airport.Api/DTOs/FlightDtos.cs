using Airport.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Airport.Api.DTOs;

public record FlightDto(
    int Id,
    string FlightNumber,
    string Origin,
    string OriginAirportCode,
    string Destination,
    string DestinationAirportCode,
    string BoardingGate,
    string AircraftCode,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    int PassengerCount,
    decimal BasePrice,
    int? GateId,
    string? GateNumber,
    string? ExternalId,
    string Source,
    DateTime LastUpdatedAt,
    string? AirlineName,
    string? AirlineIata,
    string? AircraftManufacturer,
    string? AircraftModel,
    int? SeatCapacity,
    int? ManufactureYear,
    string? OriginAirportName,
    string? OriginIataCode,
    string? OriginCountry,
    string? DestinationAirportName,
    string? DestinationIataCode,
    string? DestinationCountry);

public record CreateFlightDto(
    [Required, MaxLength(20)] string FlightNumber,
    [Required, MaxLength(80)] string Origin,
    [MaxLength(12)] string? OriginAirportCode,
    [Required, MaxLength(80)] string Destination,
    [MaxLength(12)] string? DestinationAirportCode,
    [MaxLength(20)] string? BoardingGate,
    [MaxLength(40)] string? AircraftCode,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    decimal? BasePrice,
    int? GateId,
    [MaxLength(120)] string? ExternalId,
    [Required, MaxLength(60)] string Source);

public record UpdateFlightDto(
    [Required, MaxLength(20)] string FlightNumber,
    [Required, MaxLength(80)] string Origin,
    [MaxLength(12)] string? OriginAirportCode,
    [Required, MaxLength(80)] string Destination,
    [MaxLength(12)] string? DestinationAirportCode,
    [MaxLength(20)] string? BoardingGate,
    [MaxLength(40)] string? AircraftCode,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    decimal? BasePrice,
    int? GateId,
    [MaxLength(120)] string? ExternalId,
    [Required, MaxLength(60)] string Source);
