using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Messages;
using Airport.Api.Models;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController(
    AirportDbContext dbContext,
    ICurrentUserService currentUser,
    IEmailSender emailSender,
    IWebHostEnvironment environment,
    ILogger<TicketsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketDto>>> GetTickets(CancellationToken cancellationToken)
    {
        var query = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Passenger)
            .Include(ticket => ticket.Flight)
                .ThenInclude(flight => flight.GeneratedSchedule)
            .AsQueryable();

        if (User.IsInRole(nameof(UserRole.Passenger)))
        {
            query = query.Where(ticket => ticket.PassengerId == currentUser.UserId);
        }

        var tickets = await query
            .OrderBy(ticket => ticket.Flight.DepartureTime)
            .Select(ticket => ToDto(ticket))
            .ToListAsync(cancellationToken);

        return Ok(tickets);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetTicket(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Include(item => item.Passenger)
            .Include(item => item.Flight)
                .ThenInclude(flight => flight.GeneratedSchedule)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        return Ok(ToDto(ticket));
    }

    [HttpPost]
    public async Task<ActionResult<TicketDto>> CreateTicket(CreateTicketDto request, CancellationToken cancellationToken)
    {
        var passengerId = request.PassengerId;
        if (User.IsInRole(nameof(UserRole.Passenger)))
        {
            passengerId = currentUser.UserId;
        }
        else if (!User.IsInRole(nameof(UserRole.Admin)) && !User.IsInRole(nameof(UserRole.Ticketing)))
        {
            return Forbid();
        }

        if (!passengerId.HasValue)
        {
            return ValidationProblem("PassengerId is required.");
        }

        var passenger = await dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == passengerId.Value && user.Role == UserRole.Passenger,
            cancellationToken);
        if (passenger is null)
        {
            return ValidationProblem("Passenger does not exist.");
        }

        var flight = await dbContext.Flights
            .Include(item => item.GeneratedSchedule)
            .FirstOrDefaultAsync(item => item.Id == request.FlightId, cancellationToken);
        if (flight is null)
        {
            return ValidationProblem("Flight does not exist.");
        }

        if (flight.Status is FlightStatus.Cancelled or FlightStatus.Departed)
        {
            return ValidationProblem("Cannot book this flight.");
        }

        if (User.IsInRole(nameof(UserRole.Passenger)))
        {
            if (flight.DepartureTime <= DateTime.UtcNow.AddHours(1))
            {
                return ValidationProblem("Booking closes 1 hour before departure.");
            }
        }
        else if (flight.DepartureTime <= DateTime.UtcNow.AddHours(1))
        {
            return ValidationProblem("Booking closes 1 hour before departure.");
        }

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync<ActionResult<TicketDto>>(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var seatNumber = string.IsNullOrWhiteSpace(request.SeatNumber)
                ? await PickRandomSeatAsync(flight, cancellationToken)
                : request.SeatNumber.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(seatNumber))
            {
                return ValidationProblem("No available seats on this flight.");
            }

            if (!await IsSeatAvailableAsync(flight, seatNumber, cancellationToken))
            {
                return ValidationProblem("Seat is not available on this flight.");
            }

            var baggageAllowanceKg = request.BaggageAllowanceKg.GetValueOrDefault(0);
            if (baggageAllowanceKg is not (0 or 10 or 20 or 35))
            {
                return ValidationProblem("Baggage allowance must be 0, 10, 20, or 35 kg.");
            }

            var ticket = new Ticket
            {
                PassengerId = passenger.Id,
                FlightId = request.FlightId,
                PassengerNationalId = string.IsNullOrWhiteSpace(request.PassengerNationalId)
                    ? passenger.NationalId
                    : request.PassengerNationalId.Trim(),
                PassengerEmail = string.IsNullOrWhiteSpace(request.PassengerEmail)
                    ? passenger.Email
                    : request.PassengerEmail.Trim().ToLowerInvariant(),
                SeatNumber = seatNumber,
                BaggageAllowanceKg = baggageAllowanceKg,
                Class = request.Class,
                Price = request.Price ?? CalculatePrice(flight, request.Class, baggageAllowanceKg, !string.IsNullOrWhiteSpace(request.SeatNumber)),
                Status = TicketStatus.Active,
                BookingDate = DateTime.UtcNow
            };

            dbContext.Tickets.Add(ticket);
            flight.PassengerCount += 1;
            await dbContext.SaveChangesAsync(cancellationToken);

            var eventId = Guid.NewGuid();
            var task = new TicketGenerationTask(
                eventId,
                ticket.Id,
                passenger.Id,
                flight.Id,
                passenger.FullName,
                flight.FlightNumber,
                flight.Origin,
                flight.Destination,
                flight.DepartureTime,
                ticket.SeatNumber,
                ticket.Class.ToString(),
                ticket.Price);

            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                EventId = eventId,
                Type = nameof(TicketGenerationTask),
                Payload = JsonSerializer.Serialize(task),
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await dbContext.Entry(ticket).Reference(item => item.Passenger).LoadAsync(cancellationToken);
            await dbContext.Entry(ticket).Reference(item => item.Flight).LoadAsync(cancellationToken);
            await dbContext.Entry(ticket.Flight).Reference(item => item.GeneratedSchedule).LoadAsync(cancellationToken);

            var dto = ToDto(ticket);
            await TrySendEmailAsync(BookingConfirmationEmailTemplate.Create(dto, environment), cancellationToken);

            return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, dto);
        });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<TicketDto>> CancelTicket(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.Passenger)
            .Include(item => item.Flight)
                .ThenInclude(flight => flight.GeneratedSchedule)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        if (!User.IsInRole(nameof(UserRole.Passenger)) && !User.IsInRole(nameof(UserRole.Admin)) && !User.IsInRole(nameof(UserRole.Ticketing)))
        {
            return Forbid();
        }

        ticket.Status = TicketStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        var dto = ToDto(ticket);
        await TrySendEmailAsync(
            dto.PassengerEmail,
            "KodFly ticket update",
            BuildChangeEmail(dto, "Your ticket has been cancelled."),
            cancellationToken);

        return Ok(dto);
    }

    [HttpPut("{id:int}/options")]
    public async Task<ActionResult<TicketDto>> UpdateTicketOptions(int id, UpdateTicketOptionsDto request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.Passenger)
            .Include(item => item.Flight)
                .ThenInclude(flight => flight.GeneratedSchedule)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        if (!User.IsInRole(nameof(UserRole.Passenger)) && !User.IsInRole(nameof(UserRole.Admin)) && !User.IsInRole(nameof(UserRole.Ticketing)))
        {
            return Forbid();
        }

        if (ticket.Status != TicketStatus.Active)
        {
            return ValidationProblem("Only active tickets can be changed.");
        }

        var seatNumber = request.SeatNumber.Trim().ToUpperInvariant();
        if (!await IsSeatAvailableAsync(ticket.Flight, seatNumber, cancellationToken, ticket.Id))
        {
            return ValidationProblem("Seat is not available on this flight.");
        }

        var baggageAllowanceKg = request.BaggageAllowanceKg.GetValueOrDefault(ticket.BaggageAllowanceKg);
        if (baggageAllowanceKg is not (0 or 10 or 20 or 35))
        {
            return ValidationProblem("Baggage allowance must be 0, 10, 20, or 35 kg.");
        }

        ticket.SeatNumber = seatNumber;
        ticket.Class = request.Class;
        ticket.BaggageAllowanceKg = baggageAllowanceKg;
        ticket.Price = CalculatePrice(ticket.Flight, request.Class, ticket.BaggageAllowanceKg, chooseSeat: true);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = ToDto(ticket);
        await TrySendEmailAsync(
            dto.PassengerEmail,
            "KodFly ticket update",
            BuildChangeEmail(dto, "Your ticket details have been updated."),
            cancellationToken);

        return Ok(dto);
    }

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> DownloadTicketFile(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (ticket is null)
        {
            return NotFound();
        }

        if (User.IsInRole(nameof(UserRole.Passenger)) && ticket.PassengerId != currentUser.UserId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(ticket.TicketFilePath) || !System.IO.File.Exists(ticket.TicketFilePath))
        {
            return NotFound("Ticket file has not been generated yet.");
        }

        var fileName = Path.GetFileName(ticket.TicketFilePath);
        return PhysicalFile(ticket.TicketFilePath, "text/plain", fileName);
    }

    private static TicketDto ToDto(Ticket ticket) => new(
        ticket.Id,
        ticket.PassengerId,
        ticket.Passenger.FullName,
        ticket.PassengerNationalId,
        ticket.PassengerEmail,
        ticket.FlightId,
        ticket.Flight.FlightNumber,
        ticket.Flight.GeneratedSchedule?.AirlineName,
        ticket.SeatNumber,
        ticket.BaggageAllowanceKg,
        ticket.Class,
        ticket.Price,
        ticket.Status,
        ticket.TicketFilePath,
        ticket.BookingDate,
        ticket.Flight.Origin,
        ticket.Flight.OriginAirportCode,
        ticket.Flight.GeneratedSchedule?.OriginAirportName,
        ticket.Flight.GeneratedSchedule?.OriginCountry,
        ticket.Flight.Destination,
        ticket.Flight.DestinationAirportCode,
        ticket.Flight.GeneratedSchedule?.DestinationAirportName,
        ticket.Flight.GeneratedSchedule?.DestinationCountry,
        ticket.Flight.DepartureTime,
        ticket.Flight.ArrivalTime,
        ticket.Flight.BoardingGate,
        ticket.Flight.AircraftCode,
        ticket.Flight.GeneratedSchedule?.AircraftManufacturer,
        ticket.Flight.GeneratedSchedule?.AircraftModel,
        ticket.Flight.GeneratedSchedule?.SeatCapacity,
        ticket.Flight.Status,
        ticket.Flight.BasePrice);

    private async Task<string?> PickRandomSeatAsync(Flight flight, CancellationToken cancellationToken)
    {
        var capacity = flight.GeneratedSchedule?.SeatCapacity > 0 ? flight.GeneratedSchedule.SeatCapacity : 180;
        var occupied = await dbContext.Tickets
            .Where(ticket => ticket.FlightId == flight.Id && ticket.Status == TicketStatus.Active)
            .Select(ticket => ticket.SeatNumber)
            .ToListAsync(cancellationToken);
        var occupiedSet = occupied.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seats = BuildSeats(capacity)
            .Where(seat => !occupiedSet.Contains(seat))
            .ToList();
        return seats.Count == 0 ? null : seats[Random.Shared.Next(seats.Count)];
    }

    private Task<bool> IsSeatAvailableAsync(Flight flight, string seatNumber, CancellationToken cancellationToken) =>
        IsSeatAvailableAsync(flight, seatNumber, cancellationToken, exceptTicketId: null);

    private async Task<bool> IsSeatAvailableAsync(Flight flight, string seatNumber, CancellationToken cancellationToken, int? exceptTicketId)
    {
        var capacity = flight.GeneratedSchedule?.SeatCapacity > 0 ? flight.GeneratedSchedule.SeatCapacity : 180;
        if (!BuildSeats(capacity).Contains(seatNumber, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return !await dbContext.Tickets.AnyAsync(
            ticket => ticket.FlightId == flight.Id
                && (!exceptTicketId.HasValue || ticket.Id != exceptTicketId.Value)
                && ticket.Status == TicketStatus.Active
                && ticket.SeatNumber == seatNumber,
            cancellationToken);
    }

    private static IEnumerable<string> BuildSeats(int capacity)
    {
        var letters = new[] { "A", "B", "C", "D", "E", "F" };
        for (var index = 0; index < capacity; index++)
        {
            yield return $"{index / letters.Length + 1}{letters[index % letters.Length]}";
        }
    }

    private static decimal CalculatePrice(Flight flight, TicketClass ticketClass, int baggageAllowanceKg, bool chooseSeat)
    {
        var basePrice = flight.BasePrice > 0 ? flight.BasePrice : flight.DepartureTime > DateTime.UtcNow ? 180m : 120m;
        var classPrice = ticketClass switch
        {
            TicketClass.Business => basePrice * 2.4m,
            TicketClass.First => basePrice * 4.1m,
            _ => basePrice
        };

        var baggagePrice = baggageAllowanceKg switch
        {
            10 => 35m,
            20 => 60m,
            35 => 95m,
            _ => 0m
        };

        return Math.Round(classPrice + baggagePrice + (chooseSeat ? 18m : 0m), 2);
    }

    private async Task TrySendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        await TrySendEmailAsync(new EmailMessage(to, subject, body), cancellationToken);
    }

    private async Task TrySendEmailAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Ticket email could not be sent to {Recipient}.", message.To);
        }
    }

    private static string BuildChangeEmail(TicketDto ticket, string message) =>
        $"""
        Hello {ticket.PassengerName},
        {message}

        Ticket details:
        Booking reference: KOD-{ticket.Id:000000}
        Flight: {ticket.FlightNumber}
        Route: {ticket.Origin}, {ticket.OriginCountry} -> {ticket.Destination}, {ticket.DestinationCountry}
        Departure: {ticket.DepartureTime:dd/MM/yyyy HH:mm}
        Seat: {ticket.SeatNumber}
        Class: {ticket.Class}
        Status: {ticket.Status}

        KodFly team
        """;
}
