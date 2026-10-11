using Airport.Api.Models;

namespace Airport.FlightUpdates.Consumer.Messages;

public record FlightUpdateMessage(
    string FlightNumber,
    string Origin,
    string? OriginAirportCode,
    string Destination,
    string? DestinationAirportCode,
    string? BoardingGate,
    string? AircraftCode,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    string ExternalId,
    string Source,
    decimal? BasePrice);
