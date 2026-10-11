using Airport.Api.Data;
using Airport.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Airport.Api.Services;

public static class CsvFlightScheduleSeeder
{
    private const string Source = "CsvGeneratedAllOriginsV3";

    public static async Task SeedAsync(
        AirportDbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("AIRPORT_SEED_CSV_FLIGHTS", true))
        {
            return;
        }

        if (configuration.GetValue("AIRPORT_CSV_REGENERATE_FLIGHTS", false))
        {
            await dbContext.Flights
                .Where(flight => flight.Source == Source)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (await dbContext.Flights.AnyAsync(flight => flight.Source == Source, cancellationToken))
        {
            return;
        }

        var dataDirectory = ResolveDataDirectory(configuration, environment);
        var airports = LoadAirports(Path.Combine(dataDirectory, "airports.csv"));
        var airlines = LoadAirlines(Path.Combine(dataDirectory, "Airlines.csv"));
        var aircraft = LoadAircraft(Path.Combine(dataDirectory, "aircraft.csv"));

        if (airports.Count < 2 || airlines.Count == 0 || aircraft.Count == 0)
        {
            return;
        }

        var seed = configuration.GetValue("AIRPORT_CSV_RANDOM_SEED", 20261011);
        var random = new Random(seed);
        var days = Math.Clamp(configuration.GetValue("AIRPORT_CSV_FLIGHT_DAYS", 14), 1, 60);
        var destinationsPerOrigin = Math.Clamp(configuration.GetValue("AIRPORT_CSV_DESTINATIONS_PER_ORIGIN", 5), 1, 12);
        var minFlightsPerRouteDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_MIN_FLIGHTS_PER_ROUTE_DAY", 1), 1, 10);
        var maxFlightsPerRouteDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_MAX_FLIGHTS_PER_ROUTE_DAY", 3), minFlightsPerRouteDay, 10);

        var routes = BuildRoutes(airports, destinationsPerOrigin, random);
        var generatedAt = DateTime.UtcNow;
        var startDate = DateTime.UtcNow.Date;
        var schedules = new List<GeneratedFlightSchedule>();

        for (var day = 0; day < days; day++)
        {
            var date = startDate.AddDays(day);

            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var route = routes[routeIndex];
                var dailyFlights = random.Next(minFlightsPerRouteDay, maxFlightsPerRouteDay + 1);

                for (var flightIndex = 0; flightIndex < dailyFlights; flightIndex++)
                {
                    var airline = Pick(airlines, random);
                    var airplane = Pick(aircraft, random);
                    var departure = BuildDeparture(date, flightIndex, dailyFlights, random);
                    var distanceKm = DistanceKm(route.Origin, route.Destination);
                    var arrival = departure.AddMinutes(FlightDurationMinutes(distanceKm, random));
                    var flightNumber = BuildFlightNumber(airline, random);
                    var externalId = $"csv-{date:yyyyMMdd}-{routeIndex + 1:D3}-{flightIndex + 1:D2}";

                    var flight = new Flight
                    {
                        FlightNumber = flightNumber,
                        Origin = Limit(route.Origin.DisplayCity, 80),
                        Destination = Limit(route.Destination.DisplayCity, 80),
                        DepartureTime = departure,
                        ArrivalTime = arrival,
                        Status = FlightStatusFor(departure, random),
                        GateId = random.Next(1, 4),
                        ExternalId = externalId,
                        Source = Source,
                        LastUpdatedAt = generatedAt
                    };

                    schedules.Add(new GeneratedFlightSchedule
                    {
                        Flight = flight,
                        AircraftRegistration = Limit(airplane.Registration, 40),
                        AircraftManufacturer = Limit(airplane.Manufacturer, 80),
                        AircraftModel = Limit(airplane.Model, 80),
                        SeatCapacity = airplane.SeatCapacity,
                        ManufactureYear = airplane.ManufactureYear,
                        AirlineExternalId = Limit(airline.Id, 40),
                        AirlineName = Limit(airline.Name, 160),
                        AirlineIata = LimitOrNull(airline.Iata, 8),
                        AirlineIcao = LimitOrNull(airline.Icao, 8),
                        AirlineCountry = LimitOrNull(airline.Country, 80),
                        OriginAirportExternalId = Limit(route.Origin.Id, 40),
                        OriginAirportName = Limit(route.Origin.Name, 160),
                        OriginIataCode = Limit(route.Origin.Iata, 8),
                        OriginIcaoCode = LimitOrNull(route.Origin.Icao, 8),
                        OriginCountry = LimitOrNull(CountryName(route.Origin.Country), 80),
                        DestinationAirportExternalId = Limit(route.Destination.Id, 40),
                        DestinationAirportName = Limit(route.Destination.Name, 160),
                        DestinationIataCode = Limit(route.Destination.Iata, 8),
                        DestinationIcaoCode = LimitOrNull(route.Destination.Icao, 8),
                        DestinationCountry = LimitOrNull(CountryName(route.Destination.Country), 80),
                        DistanceKm = distanceKm,
                        GeneratedAt = generatedAt
                    });
                }
            }
        }

        const int batchSize = 500;
        foreach (var batch in schedules.Chunk(batchSize))
        {
            dbContext.GeneratedFlightSchedules.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
    }

    private static string ResolveDataDirectory(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["AIRPORT_DATA_DIRECTORY"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var publishedData = Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(publishedData))
        {
            return publishedData;
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Data"));
    }

    private static List<AirportRecord> LoadAirports(string path)
    {
        return ReadCsv(path, hasHeader: true)
            .Select(row => new AirportRecord(
                Clean(row.GetValueOrDefault("id")),
                Clean(row.GetValueOrDefault("type")),
                Clean(row.GetValueOrDefault("name")),
                Clean(row.GetValueOrDefault("municipality")),
                Clean(row.GetValueOrDefault("iso_country")),
                Clean(row.GetValueOrDefault("iata_code")),
                Clean(row.GetValueOrDefault("icao_code")),
                ParseDouble(row.GetValueOrDefault("latitude_deg")),
                ParseDouble(row.GetValueOrDefault("longitude_deg")),
                Clean(row.GetValueOrDefault("scheduled_service"))))
            .Where(airport =>
                airport.ScheduledService.Equals("yes", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(airport.Iata)
                && airport.Latitude.HasValue
                && airport.Longitude.HasValue
                && airport.Type is "large_airport" or "medium_airport")
            .DistinctBy(airport => airport.Iata)
            .ToList();
    }

    private static List<AirlineRecord> LoadAirlines(string path)
    {
        return ReadCsv(path, hasHeader: false)
            .Select(row => new AirlineRecord(
                Clean(row["0"]),
                Clean(row["1"]),
                Clean(row["3"]),
                Clean(row["4"]),
                Clean(row["6"]),
                Clean(row["7"])))
            .Where(airline =>
                airline.Active.Equals("Y", StringComparison.OrdinalIgnoreCase)
                && airline.Name is not "Unknown" and not "Private flight"
                && (!string.IsNullOrWhiteSpace(airline.Iata) || !string.IsNullOrWhiteSpace(airline.Icao)))
            .ToList();
    }

    private static List<AircraftRecord> LoadAircraft(string path)
    {
        return ReadCsv(path, hasHeader: true)
            .Select(row => new AircraftRecord(
                Clean(row.GetValueOrDefault("registration")),
                Clean(row.GetValueOrDefault("manufacturer")),
                Clean(row.GetValueOrDefault("model")),
                ParseInt(row.GetValueOrDefault("seat_capacity")),
                ParseInt(row.GetValueOrDefault("manufacture_year"))))
            .Where(aircraft =>
                !string.IsNullOrWhiteSpace(aircraft.Registration)
                && !string.IsNullOrWhiteSpace(aircraft.Manufacturer)
                && !string.IsNullOrWhiteSpace(aircraft.Model)
                && aircraft.SeatCapacity > 0)
            .ToList();
    }

    private static List<RouteRecord> BuildRoutes(List<AirportRecord> airports, int destinationsPerOrigin, Random random)
    {
        var routes = new List<RouteRecord>();
        foreach (var origin in airports.OrderBy(_ => random.Next()))
        {
            var candidates = airports
                .Where(airport => airport.Iata != origin.Iata)
                .OrderBy(_ => random.Next())
                .Take(destinationsPerOrigin);

            foreach (var destination in candidates)
            {
                routes.Add(new RouteRecord(origin, destination));
            }
        }

        return routes;
    }

    private static DateTime BuildDeparture(DateTime date, int flightIndex, int dailyFlights, Random random)
    {
        var firstHour = dailyFlights switch
        {
            1 => random.Next(7, 21),
            2 => flightIndex == 0 ? random.Next(6, 11) : random.Next(16, 22),
            _ => flightIndex switch
            {
                0 => random.Next(5, 9),
                1 => random.Next(11, 16),
                _ => random.Next(18, 23)
            }
        };

        var minute = new[] { 0, 15, 30, 45 }[random.Next(4)];
        return DateTime.SpecifyKind(date.AddHours(firstHour).AddMinutes(minute), DateTimeKind.Utc);
    }

    private static int FlightDurationMinutes(int distanceKm, Random random)
    {
        var cruiseMinutes = distanceKm / 13;
        return Math.Clamp(cruiseMinutes + random.Next(35, 75), 45, 900);
    }

    private static string BuildFlightNumber(AirlineRecord airline, Random random)
    {
        var code = !string.IsNullOrWhiteSpace(airline.Iata) ? airline.Iata : airline.Icao ?? string.Empty;
        code = new string(code.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            code = "FL";
        }

        return $"{code}{random.Next(100, 9999)}";
    }

    private static FlightStatus FlightStatusFor(DateTime departure, Random random)
    {
        var now = DateTime.UtcNow;
        if (departure < now.AddHours(-2))
        {
            return FlightStatus.Departed;
        }

        if (departure <= now.AddHours(2))
        {
            return FlightStatus.Boarding;
        }

        var roll = random.NextDouble();
        if (roll < 0.02)
        {
            return FlightStatus.Cancelled;
        }

        return roll < 0.09 ? FlightStatus.Delayed : FlightStatus.Scheduled;
    }

    private static int DistanceKm(AirportRecord origin, AirportRecord destination)
    {
        const double earthRadiusKm = 6371;
        var originLat = ToRadians(origin.Latitude!.Value);
        var destinationLat = ToRadians(destination.Latitude!.Value);
        var latDelta = ToRadians(destination.Latitude.Value - origin.Latitude.Value);
        var lonDelta = ToRadians(destination.Longitude!.Value - origin.Longitude!.Value);
        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2)
            + Math.Cos(originLat) * Math.Cos(destinationLat) * Math.Sin(lonDelta / 2) * Math.Sin(lonDelta / 2);
        return Math.Max(1, (int)Math.Round(earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a))));
    }

    private static double ToRadians(double value) => value * Math.PI / 180;

    private static T Pick<T>(IReadOnlyList<T> items, Random random) => items[random.Next(items.Count)];

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;

    private static string Clean(string? value)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return cleaned is @"\N" or "N/A" ? string.Empty : cleaned;
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string? LimitOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Limit(value, maxLength);
    }

    private static string CountryName(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("ZZ", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        try
        {
            return new RegionInfo(countryCode).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryCode;
        }
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path, bool hasHeader)
    {
        using var reader = new StreamReader(path);
        var headers = hasHeader
            ? ParseCsvLine(reader.ReadLine() ?? string.Empty)
            : [];

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < values.Count; index++)
            {
                var key = hasHeader && index < headers.Count ? headers[index] : index.ToString(CultureInfo.InvariantCulture);
                row[key] = values[index];
            }

            yield return row;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Add('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(new string(current.ToArray()));
                current.Clear();
            }
            else
            {
                current.Add(character);
            }
        }

        values.Add(new string(current.ToArray()));
        return values;
    }

    private sealed record AirportRecord(
        string Id,
        string Type,
        string Name,
        string Municipality,
        string Country,
        string Iata,
        string? Icao,
        double? Latitude,
        double? Longitude,
        string ScheduledService)
    {
        public string DisplayCity => !string.IsNullOrWhiteSpace(Municipality) ? Municipality : Name;
    }

    private sealed record AirlineRecord(string Id, string Name, string? Iata, string? Icao, string? Country, string Active);

    private sealed record AircraftRecord(
        string Registration,
        string Manufacturer,
        string Model,
        int SeatCapacity,
        int ManufactureYear);

    private sealed record RouteRecord(AirportRecord Origin, AirportRecord Destination);
}
