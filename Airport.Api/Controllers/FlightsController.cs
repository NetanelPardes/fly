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
    private const string ActiveCsvSource = "CsvGeneratedAllOriginsV3";
    private static readonly string[] DemoSources = ["DemoSeed", "DemoSchedule", "PythonDemo", "CsvGenerated", "CsvGeneratedAllOrigins", "CsvGeneratedAllOriginsV2"];

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
                    || flight.GeneratedSchedule.OriginIataCode.Contains(first)
                    || flight.GeneratedSchedule.OriginAirportName.Contains(first)))
                || (second != null && ((flight.GeneratedSchedule.OriginCountry != null && flight.GeneratedSchedule.OriginCountry.Contains(second))
                    || flight.GeneratedSchedule.OriginIataCode.Contains(second)
                    || flight.GeneratedSchedule.OriginAirportName.Contains(second)))
                || (third != null && ((flight.GeneratedSchedule.OriginCountry != null && flight.GeneratedSchedule.OriginCountry.Contains(third))
                    || flight.GeneratedSchedule.OriginIataCode.Contains(third)
                    || flight.GeneratedSchedule.OriginAirportName.Contains(third))))));
        }

        if (!string.IsNullOrWhiteSpace(destinationSearch))
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
                    || flight.GeneratedSchedule.DestinationIataCode.Contains(first)
                    || flight.GeneratedSchedule.DestinationAirportName.Contains(first)))
                || (second != null && ((flight.GeneratedSchedule.DestinationCountry != null && flight.GeneratedSchedule.DestinationCountry.Contains(second))
                    || flight.GeneratedSchedule.DestinationIataCode.Contains(second)
                    || flight.GeneratedSchedule.DestinationAirportName.Contains(second)))
                || (third != null && ((flight.GeneratedSchedule.DestinationCountry != null && flight.GeneratedSchedule.DestinationCountry.Contains(third))
                    || flight.GeneratedSchedule.DestinationIataCode.Contains(third)
                    || flight.GeneratedSchedule.DestinationAirportName.Contains(third))))));
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

        Apply(request, flight);
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
        flight.LastUpdatedAt,
        flight.GeneratedSchedule?.AirlineName,
        flight.GeneratedSchedule?.AirlineIata,
        flight.GeneratedSchedule?.AircraftModel,
        flight.GeneratedSchedule?.OriginAirportName,
        flight.GeneratedSchedule?.OriginIataCode,
        flight.GeneratedSchedule?.OriginCountry,
        flight.GeneratedSchedule?.DestinationAirportName,
        flight.GeneratedSchedule?.DestinationIataCode,
        flight.GeneratedSchedule?.DestinationCountry);

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
