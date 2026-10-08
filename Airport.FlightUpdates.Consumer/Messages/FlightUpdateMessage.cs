using Airport.Api.Models;

namespace Airport.FlightUpdates.Consumer.Messages;

public record FlightUpdateMessage(
    string FlightNumber,
    string Origin,
    string Destination,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    FlightStatus Status,
    string ExternalId,
    string Source);
