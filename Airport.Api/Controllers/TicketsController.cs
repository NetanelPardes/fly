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
public class TicketsController(AirportDbContext dbContext, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TicketDto>>> GetTickets(CancellationToken cancellationToken)
    {
        var query = dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Passenger)
            .Include(ticket => ticket.Flight)
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

        var flight = await dbContext.Flights.FirstOrDefaultAsync(item => item.Id == request.FlightId, cancellationToken);
        if (flight is null)
        {
            return ValidationProblem("Flight does not exist.");
        }

        if (flight.Status == FlightStatus.Cancelled)
        {
            return ValidationProblem("Cannot book a cancelled flight.");
        }

        if (User.IsInRole(nameof(UserRole.Passenger)))
        {
            if (!string.Equals(flight.Origin, "Tel Aviv", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationProblem("Passengers can book outgoing flights only.");
            }

            if (flight.DepartureTime <= DateTime.UtcNow.AddHours(5))
            {
                return ValidationProblem("Booking closes 5 hours before departure.");
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var ticket = new Ticket
        {
            PassengerId = passenger.Id,
            FlightId = request.FlightId,
            SeatNumber = request.SeatNumber.Trim().ToUpperInvariant(),
            Class = request.Class,
            Price = request.Price,
            Status = TicketStatus.Active
        };

        dbContext.Tickets.Add(ticket);
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

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ToDto(ticket));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<TicketDto>> CancelTicket(int id, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.Passenger)
            .Include(item => item.Flight)
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
        return Ok(ToDto(ticket));
    }

    [HttpPut("{id:int}/options")]
    public async Task<ActionResult<TicketDto>> UpdateTicketOptions(int id, UpdateTicketOptionsDto request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.Passenger)
            .Include(item => item.Flight)
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

        ticket.SeatNumber = request.SeatNumber.Trim().ToUpperInvariant();
        ticket.Class = request.Class;
        ticket.Price = CalculatePrice(ticket.Flight, request.Class);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(ticket));
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
        ticket.FlightId,
        ticket.Flight.FlightNumber,
        ticket.SeatNumber,
        ticket.Class,
        ticket.Price,
        ticket.Status,
        ticket.TicketFilePath);

    private static decimal CalculatePrice(Flight flight, TicketClass ticketClass)
    {
        var basePrice = flight.DepartureTime > DateTime.UtcNow ? 180m : 120m;
        return ticketClass switch
        {
            TicketClass.Business => basePrice * 2.4m,
            TicketClass.First => basePrice * 4.1m,
            _ => basePrice
        };
    }
}
