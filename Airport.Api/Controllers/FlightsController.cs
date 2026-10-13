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
    private const int MaxConnections = 3;
    private const int MinConnectionMinutes = 60;
    private const int MaxConnectionMinutes = 24 * 60;
    private const int MaxRouteResults = 120;
    private const int MaxCandidatesPerLeg = 40;
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
        [FromQuery] bool includeDemo = false,
        [FromQuery] bool bookableOnly = false)
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

        if (bookableOnly)
        {
            var earliestBookableDeparture = DateTime.UtcNow.AddHours(1);
            query = query.Where(flight =>
                flight.DepartureTime >= earliestBookableDeparture
                && flight.Status != FlightStatus.Cancelled
                && flight.Status != FlightStatus.Departed);
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

    [HttpGet("routes")]
    public async Task<ActionResult<IEnumerable<FlightRouteDto>>> GetFlightRoutes(
        [FromQuery] string originSearch,
        [FromQuery] string destinationSearch,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(originSearch) || string.IsNullOrWhiteSpace(destinationSearch))
        {
            return ValidationProblem("Origin and destination are required.");
        }

        var start = EnsureUtc(from);
        var end = EnsureUtc(to);
        var earliestBookableDeparture = DateTime.UtcNow.AddHours(1);
        if (start < earliestBookableDeparture)
        {
            start = earliestBookableDeparture;
        }

        var query = dbContext.Flights
            .AsNoTracking()
            .Where(flight =>
                flight.DepartureTime >= start
                && flight.DepartureTime <= end
                && flight.Status != FlightStatus.Cancelled
                && flight.Status != FlightStatus.Departed);

        if (configuration.GetValue("AIRPORT_HIDE_DEMO_FLIGHTS", false)
            && await dbContext.Flights.AnyAsync(flight => !DemoSources.Contains(flight.Source), cancellationToken))
        {
            query = query.Where(flight => !DemoSources.Contains(flight.Source));
        }

        var pool = await query
            .OrderBy(flight => flight.DepartureTime)
            .Select(flight => new FlightDto(
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
                flight.Gate != null ? flight.Gate.GateNumber : null,
                flight.ExternalId,
                flight.Source,
                flight.LastUpdatedAt,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.AirlineName : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.AirlineIata : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.AircraftManufacturer : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.AircraftModel : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.SeatCapacity : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.ManufactureYear : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.OriginAirportName : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.OriginIataCode : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.OriginCountry : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.DestinationAirportName : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.DestinationIataCode : null,
                flight.GeneratedSchedule != null ? flight.GeneratedSchedule.DestinationCountry : null))
            .ToListAsync(cancellationToken);

        var originQuery = BuildLocationQuery(originSearch);
        var destinationQuery = BuildLocationQuery(destinationSearch);

        var directRoutes = pool
            .Where(flight => MatchesLocation(flight, originQuery, originSide: true) && MatchesLocation(flight, destinationQuery, originSide: false))
            .Select(flight => ToRoute([flight]))
            .ToList();

        var connectionRoutes = FindConnectionRoutes(pool, originQuery, destinationQuery, directRoutes.Count);

        return Ok(directRoutes
            .Concat(connectionRoutes)
            .OrderBy(route => route.ConnectionCount)
            .ThenBy(route => route.Legs.First().DepartureTime)
            .ThenBy(route => route.BasePrice)
            .Take(MaxRouteResults));
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

    private static IReadOnlyCollection<FlightRouteDto> FindConnectionRoutes(
        IReadOnlyList<FlightDto> pool,
        LocationQuery originQuery,
        LocationQuery destinationQuery,
        int directRouteCount)
    {
        var results = new List<FlightRouteDto>(MaxRouteResults);
        var seenRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = BuildConnectionIndex(pool);
        var starts = pool.Where(flight => MatchesLocation(flight, originQuery, originSide: true)).Take(180).ToList();

        foreach (var start in starts)
        {
            foreach (var next in NextConnectionCandidates(start, index))
            {
                if (!MatchesLocation(next, destinationQuery, originSide: false))
                {
                    continue;
                }

                AddRoute([start, next]);
                if (results.Count + directRouteCount >= MaxRouteResults)
                {
                    return OrderedResults();
                }
            }
        }

        if (results.Count > 0)
        {
            return OrderedResults();
        }

        foreach (var start in starts.Take(80))
        {
            Explore([start], new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConnectionNodeKey(start, originSide: true) });
            if (results.Count + directRouteCount >= MaxRouteResults)
            {
                break;
            }
        }

        return OrderedResults();

        IReadOnlyCollection<FlightRouteDto> OrderedResults() => results
            .OrderBy(route => route.ConnectionCount)
            .ThenBy(route => route.BasePrice)
            .ThenBy(route => route.Legs.First().DepartureTime)
            .Take(Math.Max(0, MaxRouteResults - directRouteCount))
            .ToList();

        void AddRoute(List<FlightDto> legs)
        {
            var key = string.Join('-', legs.Select(leg => leg.Id));
            if (seenRoutes.Add(key))
            {
                results.Add(ToRoute(legs));
            }
        }

        void Explore(List<FlightDto> legs, HashSet<string> visited)
        {
            var last = legs[^1];
            if (MatchesLocation(last, destinationQuery, originSide: false) && legs.Count > 1)
            {
                AddRoute(legs);
                return;
            }

            if (legs.Count >= MaxConnections + 1 || results.Count + directRouteCount >= MaxRouteResults)
            {
                return;
            }

            var destinationNode = ConnectionNodeKey(last, originSide: false);
            if (!visited.Add(destinationNode))
            {
                return;
            }

            foreach (var next in NextConnectionCandidates(last, index))
            {
                if (visited.Contains(ConnectionNodeKey(next, originSide: false)))
                {
                    continue;
                }

                Explore([.. legs, next], new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase));
                if (results.Count + directRouteCount >= MaxRouteResults)
                {
                    break;
                }
            }
        }
    }

    private static FlightRouteDto ToRoute(IReadOnlyCollection<FlightDto> legs)
    {
        var routeId = string.Join("-", legs.Select(leg => leg.Id));
        return new FlightRouteDto(
            legs.Count > 1 ? $"connection-{routeId}" : $"flight-{routeId}",
            legs.Count > 1,
            legs.Count - 1,
            legs,
            legs.Sum(leg => leg.BasePrice > 0 ? leg.BasePrice : 180m));
    }

    private static Dictionary<string, List<FlightDto>> BuildConnectionIndex(IEnumerable<FlightDto> pool)
    {
        var index = new Dictionary<string, List<FlightDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var flight in pool)
        {
            foreach (var key in ConnectionKeys(flight, originSide: true))
            {
                if (!index.TryGetValue(key, out var list))
                {
                    list = [];
                    index[key] = list;
                }

                list.Add(flight);
            }
        }

        foreach (var list in index.Values)
        {
            list.Sort((a, b) => a.DepartureTime.CompareTo(b.DepartureTime));
        }

        return index;
    }

    private static IEnumerable<FlightDto> NextConnectionCandidates(FlightDto previous, IReadOnlyDictionary<string, List<FlightDto>> index)
    {
        var earliest = previous.ArrivalTime.AddMinutes(MinConnectionMinutes);
        var latest = previous.ArrivalTime.AddMinutes(MaxConnectionMinutes);
        var candidates = new Dictionary<int, FlightDto>();

        foreach (var key in ConnectionKeys(previous, originSide: false))
        {
            if (!index.TryGetValue(key, out var options))
            {
                continue;
            }

            for (var indexInOptions = FirstDepartureAtOrAfter(options, earliest); indexInOptions < options.Count; indexInOptions++)
            {
                var next = options[indexInOptions];
                if (next.Id == previous.Id || next.DepartureTime < earliest)
                {
                    continue;
                }

                if (next.DepartureTime > latest)
                {
                    break;
                }

                candidates[next.Id] = next;
                if (candidates.Count >= MaxCandidatesPerLeg)
                {
                    break;
                }
            }

            if (candidates.Count >= MaxCandidatesPerLeg)
            {
                break;
            }
        }

        return candidates.Values;
    }

    private static int FirstDepartureAtOrAfter(IReadOnlyList<FlightDto> flights, DateTime value)
    {
        var low = 0;
        var high = flights.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (flights[middle].DepartureTime < value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static IEnumerable<string> ConnectionKeys(FlightDto flight, bool originSide)
    {
        var airport = AirportKey(flight, originSide);
        if (!string.IsNullOrWhiteSpace(airport))
        {
            yield return $"airport:{airport}";
        }

        var country = originSide ? flight.OriginCountry : flight.DestinationCountry;
        if (!string.IsNullOrWhiteSpace(country))
        {
            yield return $"country:{country.Trim().ToLowerInvariant()}";
        }

        var city = originSide ? flight.Origin : flight.Destination;
        if (!string.IsNullOrWhiteSpace(city))
        {
            yield return $"city:{city.Trim().ToLowerInvariant()}";
        }
    }

    private static string ConnectionNodeKey(FlightDto flight, bool originSide)
    {
        var country = originSide ? flight.OriginCountry : flight.DestinationCountry;
        return !string.IsNullOrWhiteSpace(country)
            ? $"country:{country.Trim().ToLowerInvariant()}"
            : $"airport:{AirportKey(flight, originSide)}";
    }

    private static string AirportKey(FlightDto flight, bool originSide)
    {
        var code = originSide
            ? flight.OriginIataCode ?? flight.OriginAirportCode
            : flight.DestinationIataCode ?? flight.DestinationAirportCode;
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code.Trim().ToLowerInvariant();
        }

        return string.Join("|", new[]
            {
                originSide ? flight.Origin : flight.Destination,
                originSide ? flight.OriginCountry : flight.DestinationCountry,
                originSide ? flight.OriginAirportName : flight.DestinationAirportName
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant()));
    }

    private sealed record LocationQuery(string? AirportCode, string[] Terms);

    private static LocationQuery BuildLocationQuery(string value) => new(
        AirportCodeSearch(value),
        SearchTerms(value).Select(term => term.ToLowerInvariant()).ToArray());

    private static bool MatchesLocation(FlightDto flight, LocationQuery query, bool originSide)
    {
        if (query.Terms.Length == 0 && query.AirportCode is null)
        {
            return true;
        }

        if (query.AirportCode is not null)
        {
            var codes = originSide
                ? new[] { flight.OriginAirportCode, flight.OriginIataCode }
                : new[] { flight.DestinationAirportCode, flight.DestinationIataCode };
            return codes.Any(code => string.Equals(code, query.AirportCode, StringComparison.OrdinalIgnoreCase));
        }

        var fields = originSide
            ? new[] { flight.Origin, flight.OriginCountry, flight.OriginAirportName }
            : new[] { flight.Destination, flight.DestinationCountry, flight.DestinationAirportName };

        return query.Terms.Any(term => fields.Any(field => field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true));
    }

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
