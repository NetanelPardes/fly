using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FlightsController(AirportDbContext dbContext, IConfiguration configuration) : ControllerBase
{
    private static readonly string[] DemoSources = ["DemoSeed", "DemoSchedule", "PythonDemo"];

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FlightDto>>> GetFlights(
        [FromQuery] string? origin,
        [FromQuery] string? destination,
        [FromQuery] FlightStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken,
        [FromQuery] bool includeDemo = false)
    {
        var query = dbContext.Flights
            .AsNoTracking()
            .Include(flight => flight.Gate)
            .AsQueryable();

        if (!includeDemo
            && configuration.GetValue("AIRPORT_HIDE_DEMO_FLIGHTS", false)
            && await dbContext.Flights.AnyAsync(flight => !DemoSources.Contains(flight.Source), cancellationToken))
        {
            query = query.Where(flight => !DemoSources.Contains(flight.Source));
        }

        if (!string.IsNullOrWhiteSpace(origin))
        {
            query = query.Where(flight => flight.Origin == origin.Trim());
        }

        if (!string.IsNullOrWhiteSpace(destination))
        {
            query = query.Where(flight => flight.Destination == destination.Trim());
        }

        if (status.HasValue)
        {
            query = query.Where(flight => flight.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(flight => flight.DepartureTime >= EnsureUtc(from.Value));
        }

        if (to.HasValue)
        {
            query = query.Where(flight => flight.DepartureTime <= EnsureUtc(to.Value));
        }

        var flights = await query
            .OrderBy(flight => flight.DepartureTime)
            .Select(flight => ToDto(flight))
            .ToListAsync(cancellationToken);

        return Ok(flights);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FlightDto>> GetFlight(int id, CancellationToken cancellationToken)
    {
        var flight = await dbContext.Flights
            .AsNoTracking()
            .Include(item => item.Gate)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return flight is null ? NotFound() : Ok(ToDto(flight));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FlightDto>> CreateFlight(CreateFlightDto request, CancellationToken cancellationToken)
    {
        if (request.GateId.HasValue && !await dbContext.Gates.AnyAsync(gate => gate.Id == request.GateId, cancellationToken))
        {
            return ValidationProblem($"Gate {request.GateId} does not exist.");
        }

        var flight = new Flight();
        Apply(request, flight);

        dbContext.Flights.Add(flight);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(flight).Reference(item => item.Gate).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetFlight), new { id = flight.Id }, ToDto(flight));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FlightDto>> UpdateFlight(int id, UpdateFlightDto request, CancellationToken cancellationToken)
    {
        if (request.GateId.HasValue && !await dbContext.Gates.AnyAsync(gate => gate.Id == request.GateId, cancellationToken))
        {
            return ValidationProblem($"Gate {request.GateId} does not exist.");
        }

        var flight = await dbContext.Flights
            .Include(item => item.Gate)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (flight is null)
        {
            return NotFound();
        }

        Apply(request, flight);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(flight).Reference(item => item.Gate).LoadAsync(cancellationToken);

        return Ok(ToDto(flight));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFlight(int id, CancellationToken cancellationToken)
    {
        var flight = await dbContext.Flights.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (flight is null)
        {
            return NotFound();
        }

        dbContext.Flights.Remove(flight);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Apply(CreateFlightDto request, Flight flight)
    {
        flight.FlightNumber = request.FlightNumber.Trim();
        flight.Origin = request.Origin.Trim();
        flight.Destination = request.Destination.Trim();
        flight.DepartureTime = request.DepartureTime;
        flight.ArrivalTime = request.ArrivalTime;
        flight.Status = request.Status;
        flight.GateId = request.GateId;
        flight.ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        flight.Source = request.Source.Trim();
    }

    private static void Apply(UpdateFlightDto request, Flight flight)
    {
        flight.FlightNumber = request.FlightNumber.Trim();
        flight.Origin = request.Origin.Trim();
        flight.Destination = request.Destination.Trim();
        flight.DepartureTime = request.DepartureTime;
        flight.ArrivalTime = request.ArrivalTime;
        flight.Status = request.Status;
        flight.GateId = request.GateId;
        flight.ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        flight.Source = request.Source.Trim();
    }

    private static FlightDto ToDto(Flight flight) => new(
        flight.Id,
        flight.FlightNumber,
        flight.Origin,
        flight.Destination,
        flight.DepartureTime,
        flight.ArrivalTime,
        flight.Status,
        flight.GateId,
        flight.Gate?.GateNumber,
        flight.ExternalId,
        flight.Source,
        flight.LastUpdatedAt);

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
