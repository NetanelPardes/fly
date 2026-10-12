using Airport.Api.Data;
using Airport.Api.DTOs;
using Airport.Api.Models;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FlightsController(AirportDbContext dbContext, IConfiguration configuration) : ControllerBase
{
    private const string ActiveCsvSource = CsvFlightScheduleSeeder.Source;
    private static readonly string[] DemoSources = ["DemoSeed", "DemoSchedule", "PythonDemo", "CsvGenerated", "CsvGeneratedAllOrigins", "CsvGeneratedAllOriginsV2", "CsvGeneratedAllOriginsV4", "CsvGeneratedTrafficProfileV1"];

    [HttpGet("schedule-status")]
    public async Task<ActionResult<object>> GetScheduleStatus(CancellationToken cancellationToken)
    {
        var count = await dbContext.Flights.CountAsync(flight => flight.Source == ActiveCsvSource, cancellationToken);
        return Ok(new { ready = count > 0, flightCount = count });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FlightDto>>> GetFlights(
        [FromQuery] string? origin,
        [FromQuery] string? destination,
        [FromQuery] string? originSearch,
        [FromQuery] string? destinationSearch,
        [FromQuery] FlightStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken,
        [FromQuery] bool includeDemo = false)
    {
        var query = dbContext.Flights
            .AsNoTracking()
            .Include(flight => flight.Gate)
            .Include(flight => flight.GeneratedSchedule)
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

        if (!string.IsNullOrWhiteSpace(originSearch))
        {
            var airportCode = AirportCodeSearch(originSearch);
            if (airportCode is not null)
            {
                query = query.Where(flight =>
                    flight.OriginAirportCode == airportCode
                    || (flight.GeneratedSchedule != null
                        && (flight.GeneratedSchedule.OriginIataCode == airportCode
                            || flight.GeneratedSchedule.OriginIcaoCode == airportCode)));
            }
            else
            {
                var terms = SearchTerms(originSearch);
                var first = terms.ElementAtOrDefault(0);
                var second = terms.ElementAtOrDefault(1);
                var third = terms.ElementAtOrDefault(2);
                query = query.Where(flight =>
                    (first != null && flight.Origin.Contains(first))
                    || (second != null && flight.Origin.Contains(second))
                    || (third != null && flight.Origin.Contains(third))
                    || (flight.GeneratedSchedule != null
                    && ((first != null && ((flight.GeneratedSchedule.OriginCountry != null && flight.GeneratedSchedule.OriginCountry.Contains(first))
                        || flight.GeneratedSchedule.OriginAirportName.Contains(first)))
                    || (second != null && ((flight.GeneratedSchedule.OriginCountry != null && flight.GeneratedSchedule.OriginCountry.Contains(second))
                        || flight.GeneratedSchedule.OriginAirportName.Contains(second)))
                    || (third != null && ((flight.GeneratedSchedule.OriginCountry != null && flight.GeneratedSchedule.OriginCountry.Contains(third))
                        || flight.GeneratedSchedule.OriginAirportName.Contains(third))))));
            }
        }

        if (!string.IsNullOrWhiteSpace(destinationSearch))
        {
            var airportCode = AirportCodeSearch(destinationSearch);
            if (airportCode is not null)
            {
                query = query.Where(flight =>
                    flight.DestinationAirportCode == airportCode
                    || (flight.GeneratedSchedule != null
                        && (flight.GeneratedSchedule.DestinationIataCode == airportCode
                            || flight.GeneratedSchedule.DestinationIcaoCode == airportCode)));
            }
            else
            {
                var terms = SearchTerms(destinationSearch);
                var first = terms.ElementAtOrDefault(0);
                var second = terms.ElementAtOrDefault(1);
                var third = terms.ElementAtOrDefault(2);
                query = query.Where(flight =>
                    (first != null && flight.Destination.Contains(first))
                    || (second != null && flight.Destination.Contains(second))
                    || (third != null && flight.Destination.Contains(third))
                    || (flight.GeneratedSchedule != null
                    && ((first != null && ((flight.GeneratedSchedule.DestinationCountry != null && flight.GeneratedSchedule.DestinationCountry.Contains(first))
                        || flight.GeneratedSchedule.DestinationAirportName.Contains(first)))
                    || (second != null && ((flight.GeneratedSchedule.DestinationCountry != null && flight.GeneratedSchedule.DestinationCountry.Contains(second))
                        || flight.GeneratedSchedule.DestinationAirportName.Contains(second)))
                    || (third != null && ((flight.GeneratedSchedule.DestinationCountry != null && flight.GeneratedSchedule.DestinationCountry.Contains(third))
                        || flight.GeneratedSchedule.DestinationAirportName.Contains(third))))));
            }
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
            .Include(item => item.GeneratedSchedule)
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
        await dbContext.Entry(flight).Reference(item => item.GeneratedSchedule).LoadAsync(cancellationToken);

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

        var previousStatus = flight.Status;
        Apply(request, flight);
        if (previousStatus != FlightStatus.Cancelled && flight.Status == FlightStatus.Cancelled)
        {
            await ReassignActiveTicketsFromCancelledFlightAsync(flight, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(flight).Reference(item => item.Gate).LoadAsync(cancellationToken);
        await dbContext.Entry(flight).Reference(item => item.GeneratedSchedule).LoadAsync(cancellationToken);

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
        flight.OriginAirportCode = NormalizeCode(request.OriginAirportCode, request.Origin);
        flight.Destination = request.Destination.Trim();
        flight.DestinationAirportCode = NormalizeCode(request.DestinationAirportCode, request.Destination);
        flight.BoardingGate = string.IsNullOrWhiteSpace(request.BoardingGate) ? string.Empty : request.BoardingGate.Trim();
        flight.AircraftCode = string.IsNullOrWhiteSpace(request.AircraftCode) ? string.Empty : request.AircraftCode.Trim();
        flight.DepartureTime = request.DepartureTime;
        flight.ArrivalTime = request.ArrivalTime;
        flight.Status = request.Status;
        flight.BasePrice = request.BasePrice.GetValueOrDefault(180m);
        flight.GateId = request.GateId;
        flight.ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        flight.Source = request.Source.Trim();
    }

    private static void Apply(UpdateFlightDto request, Flight flight)
    {
        flight.FlightNumber = request.FlightNumber.Trim();
        flight.Origin = request.Origin.Trim();
        flight.OriginAirportCode = NormalizeCode(request.OriginAirportCode, request.Origin);
        flight.Destination = request.Destination.Trim();
        flight.DestinationAirportCode = NormalizeCode(request.DestinationAirportCode, request.Destination);
        flight.BoardingGate = string.IsNullOrWhiteSpace(request.BoardingGate) ? string.Empty : request.BoardingGate.Trim();
        flight.AircraftCode = string.IsNullOrWhiteSpace(request.AircraftCode) ? string.Empty : request.AircraftCode.Trim();
        flight.DepartureTime = request.DepartureTime;
        flight.ArrivalTime = request.ArrivalTime;
        flight.Status = request.Status;
        flight.BasePrice = request.BasePrice.GetValueOrDefault(180m);
        flight.GateId = request.GateId;
        flight.ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        flight.Source = request.Source.Trim();
    }

    private static FlightDto ToDto(Flight flight) => new(
        flight.Id,
        flight.FlightNumber,
        flight.Origin,
        flight.OriginAirportCode,
        flight.Destination,
        flight.DestinationAirportCode,
        flight.BoardingGate,
        flight.AircraftCode,
        flight.DepartureTime,
        flight.ArrivalTime,
        flight.Status,
        flight.PassengerCount,
        flight.BasePrice,
        flight.GateId,
        flight.Gate?.GateNumber,
        flight.ExternalId,
        flight.Source,
        flight.LastUpdatedAt,
        flight.GeneratedSchedule?.AirlineName,
        flight.GeneratedSchedule?.AirlineIata,
        flight.GeneratedSchedule?.AircraftManufacturer,
        flight.GeneratedSchedule?.AircraftModel,
        flight.GeneratedSchedule?.SeatCapacity,
        flight.GeneratedSchedule?.ManufactureYear,
        flight.GeneratedSchedule?.OriginAirportName,
        flight.GeneratedSchedule?.OriginIataCode,
        flight.GeneratedSchedule?.OriginCountry,
        flight.GeneratedSchedule?.DestinationAirportName,
        flight.GeneratedSchedule?.DestinationIataCode,
        flight.GeneratedSchedule?.DestinationCountry);

    private static string NormalizeCode(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim().ToUpperInvariant();
        }

        var letters = new string(fallback.Where(char.IsLetterOrDigit).Take(3).ToArray());
        return letters.ToUpperInvariant();
    }

    private async Task ReassignActiveTicketsFromCancelledFlightAsync(Flight cancelledFlight, CancellationToken cancellationToken)
    {
        var tickets = await dbContext.Tickets
            .Include(ticket => ticket.Passenger)
            .Where(ticket => ticket.FlightId == cancelledFlight.Id && ticket.Status == TicketStatus.Active)
            .OrderBy(ticket => ticket.BookingDate)
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return;
        }

        var candidates = await dbContext.Flights
            .Include(flight => flight.GeneratedSchedule)
            .Include(flight => flight.Tickets)
            .Where(flight =>
                flight.Id != cancelledFlight.Id
                && flight.Status != FlightStatus.Cancelled
                && flight.DepartureTime > cancelledFlight.DepartureTime
                && flight.OriginAirportCode == cancelledFlight.OriginAirportCode
                && flight.DestinationAirportCode == cancelledFlight.DestinationAirportCode)
            .OrderBy(flight => flight.DepartureTime)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            var reassigned = false;
            foreach (var candidate in candidates)
            {
                var seat = PickAvailableSeat(candidate);
                if (seat is null)
                {
                    continue;
                }

                ticket.FlightId = candidate.Id;
                ticket.SeatNumber = seat;
                candidate.PassengerCount += 1;
                reassigned = true;
                break;
            }

            if (!reassigned)
            {
                ticket.Status = TicketStatus.Cancelled;
            }
        }

        cancelledFlight.PassengerCount = 0;
    }

    private static string? PickAvailableSeat(Flight flight)
    {
        var capacity = flight.GeneratedSchedule?.SeatCapacity > 0 ? flight.GeneratedSchedule.SeatCapacity : 180;
        var occupied = flight.Tickets
            .Where(ticket => ticket.Status == TicketStatus.Active)
            .Select(ticket => ticket.SeatNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < capacity; index++)
        {
            var seat = $"{index / 6 + 1}{"ABCDEF"[index % 6]}";
            if (!occupied.Contains(seat))
            {
                occupied.Add(seat);
                return seat;
            }
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string[] SearchTerms(string value)
    {
        var trimmed = value.Trim();
        var withoutCode = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s*\([A-Za-z0-9]{2,4}\)\s*$", "");
        var terms = withoutCode
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(withoutCode)
            .ToList();

        foreach (var term in terms.ToArray())
        {
            var countryCode = CountryCodeFor(term);
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                terms.Add(countryCode);
            }
        }

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToArray();
    }

    private static string? AirportCodeSearch(string value)
    {
        var trimmed = value.Trim();
        var codeMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"\(([A-Za-z0-9]{2,4})\)\s*$");
        var candidate = codeMatch.Success ? codeMatch.Groups[1].Value : trimmed;
        return System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^[A-Za-z0-9]{2,4}$")
            ? candidate.ToUpperInvariant()
            : null;
    }

    private static string? CountryCodeFor(string countryName)
    {
        foreach (var culture in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new System.Globalization.RegionInfo(culture.Name);
                if (region.EnglishName.Equals(countryName, StringComparison.OrdinalIgnoreCase))
                {
                    return region.TwoLetterISORegionName;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return null;
    }
}
