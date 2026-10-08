namespace Airport.Api.Messages;

public record TicketGenerationTask(
    Guid EventId,
    int TicketId,
    int PassengerId,
    int FlightId,
    string PassengerName,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTime DepartureTime,
    string SeatNumber,
    string Class,
    decimal Price);
