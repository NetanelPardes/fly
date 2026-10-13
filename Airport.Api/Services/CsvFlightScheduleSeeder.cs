using Airport.Api.Data;
using Airport.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Airport.Api.Services;

public static class CsvFlightScheduleSeeder
{
    public const string Source = "CsvGeneratedTrafficProfileV4";
    private static readonly string[] PreferredHighTrafficOriginTerms =
    [
        "Paris", "Nice", "Lyon", "Marseille", "Bordeaux", "Strasbourg", "Toulouse", "Lille", "Nantes", "Montpellier",
        "Rome", "Venice", "Florence", "Milan", "Naples", "Verona", "Bologna", "Turin", "Genoa", "Pisa", "Palermo",
        "Madrid", "Barcelona", "Seville", "Valencia", "Granada", "Cordoba", "Malaga", "Bilbao", "Alicante",
        "Lisbon", "Porto", "Faro", "Funchal", "London", "Edinburgh", "Manchester", "Liverpool", "Bristol", "Birmingham",
        "Dublin", "Cork", "Berlin", "Munich", "Hamburg", "Frankfurt", "Cologne", "Dresden", "Leipzig", "Stuttgart",
        "Amsterdam", "Rotterdam", "Brussels", "Antwerp", "Zurich", "Geneva", "Lucerne", "Bern", "Basel", "Vienna",
        "Salzburg", "Prague", "Krakow", "Warsaw", "Gdansk", "Budapest", "Bratislava", "Ljubljana", "Dubrovnik",
        "Split", "Zagreb", "Athens", "Thessaloniki", "Heraklion", "Rhodes", "Corfu", "Copenhagen", "Stockholm",
        "Gothenburg", "Oslo", "Bergen", "Helsinki", "Reykjavik", "Tallinn", "Riga", "Vilnius", "Bucharest", "Sofia",
        "Belgrade", "Sarajevo", "Tirana", "Skopje", "Valletta", "Paphos", "Larnaca", "Istanbul", "Antalya", "Izmir",
        "Ankara", "Bodrum", "Tbilisi", "Batumi", "Yerevan", "Baku", "Samarkand", "Tashkent", "Almaty", "Tel Aviv",
        "Eilat", "Amman", "Aqaba", "Dubai", "Abu Dhabi", "Doha", "Riyadh", "Jeddah", "Muscat", "Cairo", "Alexandria",
        "Luxor", "Aswan", "Sharm", "Hurghada", "Marrakesh", "Marrakech", "Fes", "Casablanca", "Rabat", "Tangier",
        "Tunis", "Algiers", "Cape Town", "Johannesburg", "Durban", "Pretoria", "Nairobi", "Mombasa", "Dar es Salaam",
        "Arusha", "Zanzibar", "Kigali", "Kampala", "Addis Ababa", "Dakar", "Accra", "Port Louis", "Victoria",
        "Delhi", "Mumbai", "Agra", "Jaipur", "Udaipur", "Jodhpur", "Varanasi", "Kolkata", "Chennai", "Bengaluru",
        "Bangalore", "Hyderabad", "Kochi", "Amritsar", "Kathmandu", "Pokhara", "Colombo", "Male", "Lahore",
        "Islamabad", "Bangkok", "Chiang Mai", "Pattaya", "Phuket", "Krabi", "Hanoi", "Ho Chi Minh", "Da Nang",
        "Hoi An", "Nha Trang", "Siem Reap", "Phnom Penh", "Luang Prabang", "Vientiane", "Kuala Lumpur", "George Town",
        "Malacca", "Kota Kinabalu", "Singapore", "Jakarta", "Yogyakarta", "Denpasar", "Bandung", "Manila", "Cebu",
        "Tokyo", "Kyoto", "Osaka", "Nara", "Hiroshima", "Sapporo", "Fukuoka", "Yokohama", "Nagasaki", "Seoul",
        "Busan", "Jeju", "Beijing", "Shanghai", "Xi'an", "Chengdu", "Guangzhou", "Shenzhen", "Hangzhou", "Xiamen",
        "Sydney", "Melbourne", "Brisbane", "Perth", "Adelaide", "Auckland", "Wellington", "Christchurch", "New York",
        "Los Angeles", "Las Vegas", "Orlando", "Miami", "San Francisco", "Washington", "Chicago", "Boston",
        "New Orleans", "San Diego", "Seattle", "Honolulu", "Nashville", "Savannah", "Toronto", "Vancouver",
        "Montreal", "Quebec", "Mexico City", "Cancun", "Puerto Vallarta", "Oaxaca", "Rio de Janeiro", "Salvador",
        "Buenos Aires", "Lima", "Cusco", "Cartagena", "Medellin", "Santiago", "Quito", "Montevideo", "Havana",
        "Santo Domingo"
    ];

    public static async Task SeedAsync(
        AirportDbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        LogSeed(configuration, "CSV schedule seeding started.");
        if (!configuration.GetValue("AIRPORT_SEED_CSV_FLIGHTS", true))
        {
            LogSeed(configuration, "CSV schedule seeding skipped because AIRPORT_SEED_CSV_FLIGHTS is false.");
            return;
        }

        await SeedAirportsFromCsvAsync(dbContext, configuration, environment, cancellationToken);
        await SeedAircraftFromCsvAsync(dbContext, configuration, environment, cancellationToken);

        if (configuration.GetValue("AIRPORT_CSV_REGENERATE_FLIGHTS", false))
        {
            await dbContext.Flights
                .Where(flight => flight.Source == Source)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (await dbContext.Flights.AnyAsync(flight => flight.Source == Source, cancellationToken))
        {
            LogSeed(configuration, $"Flight generation skipped. Existing flights found for source {Source}.");
            return;
        }

        var airports = await LoadAirportsFromDatabaseAsync(dbContext, cancellationToken);
        var maxAircraftForScheduling = Math.Clamp(configuration.GetValue("AIRPORT_CSV_MAX_AIRCRAFT_FOR_SCHEDULE", 50000), 100, 50000);
        var aircraft = await LoadAircraftFromDatabaseAsync(dbContext, maxAircraftForScheduling, cancellationToken);
        if (airports.Count < 2 || aircraft.Count == 0)
        {
            LogSeed(configuration, $"Flight generation skipped. Airports: {airports.Count}, aircraft: {aircraft.Count}.");
            return;
        }

        var seed = configuration.GetValue("AIRPORT_CSV_RANDOM_SEED", 20261011);
        var random = new Random(seed);
        var days = Math.Clamp(configuration.GetValue("AIRPORT_CSV_FLIGHT_DAYS", 30), 1, 60);
        var highTrafficOriginCount = Math.Clamp(configuration.GetValue("AIRPORT_CSV_HIGH_TRAFFIC_ORIGIN_COUNT", 500), 1, airports.Count);
        var highTrafficFlightsPerDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_HIGH_TRAFFIC_FLIGHTS_PER_DAY", 50), 1, 200);
        var lowTrafficFlightsPerDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_LOW_TRAFFIC_FLIGHTS_PER_DAY", 2), 1, 20);
        var guaranteedOriginFlightsPerDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_GUARANTEED_ORIGIN_FLIGHTS_PER_DAY", 25), 1, 80);
        var maxFlightsPerOriginPerDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_MAX_FLIGHTS_PER_ORIGIN_PER_DAY", 8), 1, 20);
        var originsPerDay = Math.Clamp(configuration.GetValue("AIRPORT_CSV_ORIGINS_PER_DAY", 180), 10, airports.Count);
        var destinationsPerOrigin = Math.Clamp(configuration.GetValue("AIRPORT_CSV_DESTINATIONS_PER_ORIGIN", 25), 1, 50);

        var highTrafficOrigins = PickHighTrafficOrigins(airports, highTrafficOriginCount, random).ToHashSet();
        var origins = airports.OrderBy(_ => random.Next()).ToList();
        var guaranteedOrigins = origins.Where(IsGuaranteedDailyOrigin).ToList();
        var guaranteedOriginCodes = guaranteedOrigins.Select(origin => origin.Iata).ToHashSet(StringComparer.OrdinalIgnoreCase);
        LogSeed(configuration, $"Generating flights for {days} days. Daily origins: {originsPerDay} plus {guaranteedOrigins.Count} guaranteed origins. Guaranteed origins: {guaranteedOriginFlightsPerDay} flights/day. High traffic cap: {maxFlightsPerOriginPerDay} flights/day. Other origins: {lowTrafficFlightsPerDay} flights/day.");
        var generatedAt = DateTime.UtcNow;
        var startDate = DateTime.UtcNow.Date;
        var pendingSchedules = new List<GeneratedFlightSchedule>(500);
        var originFlightCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var aircraftNextAvailable = aircraft.ToDictionary(item => item.Registration, _ => startDate);
        var occupiedDepartureSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedFlights = 0;
        var routeSequence = 0;

        for (var day = 0; day < days; day++)
        {
            var date = startDate.AddDays(day);
            var dailyOrigins = guaranteedOrigins
                .Concat(origins.Where(origin => !guaranteedOriginCodes.Contains(origin.Iata)).OrderBy(_ => random.Next()).Take(originsPerDay))
                .DistinctBy(origin => origin.Iata)
                .ToList();

            foreach (var origin in dailyOrigins)
            {
                var destinations = PickDestinations(origin, airports, destinationsPerOrigin, random);
                var isGuaranteedDailyOrigin = IsGuaranteedDailyOrigin(origin);
                var dailyFlights = isGuaranteedDailyOrigin
                    ? guaranteedOriginFlightsPerDay
                    : highTrafficOrigins.Contains(origin)
                    ? Math.Min(highTrafficFlightsPerDay, maxFlightsPerOriginPerDay)
                    : lowTrafficFlightsPerDay;
                for (var flightIndex = 0; flightIndex < dailyFlights; flightIndex++)
                {
                    var destination = destinations[flightIndex % destinations.Count];
                    var departure = BuildDeparture(date, flightIndex, dailyFlights, random);
                    var airplane = isGuaranteedDailyOrigin
                        ? aircraft[random.Next(aircraft.Count)]
                        : FindAvailableAircraft(aircraft, aircraftNextAvailable, departure, random);
                    if (airplane is null || !occupiedDepartureSlots.Add($"{origin.Iata}|{departure:O}"))
                    {
                        continue;
                    }

                    var distanceKm = DistanceKm(origin, destination);
                    var arrival = departure.AddMinutes(FlightDurationMinutes(distanceKm, random));
                    aircraftNextAvailable[airplane.Registration] = arrival.AddHours(10);
                    routeSequence++;

                    var flight = new Flight
                    {
                        FlightNumber = BuildFlightNumber(origin.Iata, random),
                        Origin = Limit(origin.DisplayCity, 80),
                        OriginAirportCode = Limit(origin.Iata, 12),
                        Destination = Limit(destination.DisplayCity, 80),
                        DestinationAirportCode = Limit(destination.Iata, 12),
                        BoardingGate = BuildBoardingGate(origin, random),
                        AircraftCode = Limit(airplane.Registration, 40),
                        DepartureTime = departure,
                        ArrivalTime = arrival,
                        Status = FlightStatusFor(departure, random),
                        GateId = random.Next(1, 4),
                        BasePrice = BuildBasePrice(distanceKm),
                        ExternalId = $"csv-{date:yyyyMMdd}-{routeSequence:D6}-{flightIndex + 1:D2}",
                        Source = Source,
                        LastUpdatedAt = generatedAt
                    };

                    pendingSchedules.Add(new GeneratedFlightSchedule
                    {
                        Flight = flight,
                        AircraftRegistration = Limit(airplane.Registration, 40),
                        AircraftManufacturer = Limit(airplane.Manufacturer, 80),
                        AircraftModel = Limit(airplane.Model, 80),
                        SeatCapacity = airplane.SeatCapacity,
                        ManufactureYear = airplane.ManufactureYear,
                        AirlineExternalId = "SIM",
                        AirlineName = "Airport Simulation",
                        AirlineIata = "AS",
                        AirlineIcao = "AMS",
                        AirlineCountry = "Simulation",
                        OriginAirportExternalId = Limit(origin.Id, 40),
                        OriginAirportName = Limit(origin.Name, 160),
                        OriginIataCode = Limit(origin.Iata, 8),
                        OriginIcaoCode = LimitOrNull(origin.Icao, 8),
                        OriginCountry = LimitOrNull(origin.Country, 80),
                        DestinationAirportExternalId = Limit(destination.Id, 40),
                        DestinationAirportName = Limit(destination.Name, 160),
                        DestinationIataCode = Limit(destination.Iata, 8),
                        DestinationIcaoCode = LimitOrNull(destination.Icao, 8),
                        DestinationCountry = LimitOrNull(destination.Country, 80),
                        DistanceKm = distanceKm,
                        GeneratedAt = generatedAt
                    });

                    originFlightCounts[origin.Iata] = originFlightCounts.GetValueOrDefault(origin.Iata) + 1;

                    if (pendingSchedules.Count >= 500)
                    {
                        savedFlights += await SaveScheduleBatchAsync(dbContext, configuration, pendingSchedules, savedFlights, cancellationToken);
                    }
                }
            }
        }

        if (pendingSchedules.Count > 0)
        {
            savedFlights += await SaveScheduleBatchAsync(dbContext, configuration, pendingSchedules, savedFlights, cancellationToken);
        }

        foreach (var originGroup in originFlightCounts.OrderBy(item => item.Key))
        {
            LogSeed(configuration, $"Prepared {originGroup.Value} flights for origin airport {originGroup.Key}.");
        }

        LogSeed(configuration, $"Flight generation completed. Generated {savedFlights} flights.");
    }

    public static async Task SeedAirportsFromCsvAsync(AirportDbContext dbContext, IConfiguration configuration, IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        var airportCsvPath = ResolveAirportCsvPath(configuration, environment);
        if (await dbContext.AirportLocations.AnyAsync(cancellationToken))
        {
            var needsCountryRefresh = await dbContext.AirportLocations.AnyAsync(airport => airport.Country.Length <= 2, cancellationToken);
            var updated = needsCountryRefresh
                ? await RefreshAirportDetailsAsync(dbContext, airportCsvPath, configuration, cancellationToken)
                : 0;
            LogSeed(configuration, $"Airport import skipped. Existing airport rows found. Updated {updated} existing airport details from CSV.");
            return;
        }

        var airports = LoadAirports(airportCsvPath);
        var existingCodes = await dbContext.AirportLocations.Select(airport => airport.Code).ToListAsync(cancellationToken);
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var batchSize = Math.Clamp(configuration.GetValue("AIRPORT_CSV_REFERENCE_BATCH_SIZE", 1000), 100, 5000);
        var inserted = 0;

        var newAirports = airports
            .Where(airport => !existingCodeSet.Contains(airport.Iata))
            .Select(airport => new AirportLocation
            {
                Code = Limit(airport.Iata, 12),
                City = Limit(airport.DisplayCity, 120),
                Country = Limit(airport.Country, 120),
                Name = Limit(airport.Name, 180)
            });

        foreach (var batch in newAirports.Chunk(batchSize))
        {
            dbContext.AirportLocations.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            inserted += batch.Length;
            LogSeed(configuration, $"Inserted airport batch. Total airports inserted: {inserted} / {airports.Count}.");
            await Task.Delay(25, cancellationToken);
        }

        LogSeed(configuration, $"Airport import completed. Inserted {inserted} airports from {airportCsvPath}.");
    }

    public static async Task SeedAircraftFromCsvAsync(AirportDbContext dbContext, IConfiguration configuration, IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Aircraft.AnyAsync(cancellationToken))
        {
            LogSeed(configuration, "Aircraft import skipped. Existing aircraft rows found.");
            return;
        }

        var maxAircraft = Math.Clamp(configuration.GetValue("AIRPORT_CSV_MAX_AIRCRAFT_IMPORT", 50000), 100, 50000);
        var aircraft = LoadAircraft(Path.Combine(ResolveDataDirectory(configuration, environment), "aircraft.csv"))
            .Take(maxAircraft)
            .ToList();
        var existingCodes = await dbContext.Aircraft.Select(item => item.Code).ToListAsync(cancellationToken);
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var batchSize = Math.Clamp(configuration.GetValue("AIRPORT_CSV_REFERENCE_BATCH_SIZE", 1000), 100, 5000);
        var inserted = 0;

        var newAircraft = aircraft
            .Where(item => !existingCodeSet.Contains(item.Registration))
            .Select(item => new Aircraft
            {
                Code = Limit(item.Registration, 40),
                Manufacturer = Limit(item.Manufacturer, 80),
                Model = Limit(item.Model, 80),
                SeatCount = item.SeatCapacity,
                ManufactureYear = item.ManufactureYear
            });

        foreach (var batch in newAircraft.Chunk(batchSize))
        {
            dbContext.Aircraft.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            inserted += batch.Length;
            LogSeed(configuration, $"Inserted aircraft batch. Total aircraft inserted: {inserted} / {aircraft.Count}.");
            await Task.Delay(25, cancellationToken);
        }

        LogSeed(configuration, $"Aircraft import completed. Inserted {inserted} aircraft.");
    }

    private static async Task<List<AirportRecord>> LoadAirportsFromDatabaseAsync(AirportDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.AirportLocations
            .AsNoTracking()
            .OrderBy(airport => airport.Code)
            .Select(airport => new AirportRecord(airport.Code, airport.Name, airport.City, airport.Country, airport.Code, null, null, null))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<AircraftRecord>> LoadAircraftFromDatabaseAsync(AirportDbContext dbContext, int take, CancellationToken cancellationToken)
    {
        return await dbContext.Aircraft
            .AsNoTracking()
            .OrderBy(aircraft => aircraft.Code)
            .Take(take)
            .Select(aircraft => new AircraftRecord(aircraft.Code, aircraft.Manufacturer, aircraft.Model, aircraft.SeatCount, aircraft.ManufactureYear))
            .ToListAsync(cancellationToken);
    }

    private static string ResolveDataDirectory(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["AIRPORT_DATA_DIRECTORY"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var publishedData = Path.Combine(AppContext.BaseDirectory, "Data");
        return Directory.Exists(publishedData)
            ? publishedData
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "Data"));
    }

    private static string ResolveAirportCsvPath(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["AIRPORT_CSV_PATH"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var dataDirectory = ResolveDataDirectory(configuration, environment);
        var airportPath = Path.Combine(dataDirectory, "airport.csv");
        return File.Exists(airportPath)
            ? airportPath
            : Path.Combine(dataDirectory, "airports.csv");
    }

    private static async Task<int> RefreshAirportDetailsAsync(AirportDbContext dbContext, string airportCsvPath, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!File.Exists(airportCsvPath))
        {
            LogSeed(configuration, $"Airport CSV not found for refresh: {airportCsvPath}");
            return 0;
        }

        var airportsByCode = LoadAirports(airportCsvPath).ToDictionary(airport => airport.Iata, StringComparer.OrdinalIgnoreCase);
        var existingAirports = await dbContext.AirportLocations.ToListAsync(cancellationToken);
        var updated = 0;

        foreach (var airport in existingAirports)
        {
            if (!airportsByCode.TryGetValue(airport.Code, out var source))
            {
                continue;
            }

            var countryNeedsRefresh = airport.Country.Length <= 2 && source.Country.Length > 2;
            var nameNeedsRefresh = string.IsNullOrWhiteSpace(airport.Name) || !string.Equals(airport.Name, source.Name, StringComparison.Ordinal);
            var cityNeedsRefresh = string.IsNullOrWhiteSpace(airport.City) || !string.Equals(airport.City, source.DisplayCity, StringComparison.Ordinal);
            if (!countryNeedsRefresh && !nameNeedsRefresh && !cityNeedsRefresh)
            {
                continue;
            }

            airport.Country = Limit(source.Country, 120);
            airport.City = Limit(source.DisplayCity, 120);
            airport.Name = Limit(source.Name, 180);
            updated++;
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        return updated;
    }

    private static List<AirportRecord> LoadAirports(string path)
    {
        return ReadCsv(path, hasHeader: true)
            .Select(row =>
            {
                var code = Clean(First(row, "3", "iata_code", "ident", "id"));
                return new AirportRecord(
                    string.IsNullOrWhiteSpace(code) ? Clean(First(row, "id", "ident")) : code,
                    Clean(First(row, "2", "name")),
                    Clean(First(row, "0", "municipality", "city")),
                    Clean(First(row, "1", "iso_country", "country")),
                    code,
                    Clean(First(row, "icao_code", "ident")),
                    ParseDouble(First(row, "latitude_deg")),
                    ParseDouble(First(row, "longitude_deg")));
            })
            .Where(airport => !string.IsNullOrWhiteSpace(airport.Iata) && !string.IsNullOrWhiteSpace(airport.Name) && !string.IsNullOrWhiteSpace(airport.DisplayCity))
            .DistinctBy(airport => airport.Iata)
            .ToList();
    }

    private static List<AircraftRecord> LoadAircraft(string path)
    {
        return ReadCsv(path, hasHeader: true)
            .Select(row => new AircraftRecord(
                Clean(First(row, "0", "registration")),
                Clean(First(row, "1", "manufacturer")),
                Clean(First(row, "2", "model")),
                ParseInt(First(row, "3", "seat_capacity")),
                ParseInt(First(row, "4", "manufacture_year"))))
            .Where(aircraft => !string.IsNullOrWhiteSpace(aircraft.Registration) && !string.IsNullOrWhiteSpace(aircraft.Manufacturer) && !string.IsNullOrWhiteSpace(aircraft.Model) && aircraft.SeatCapacity > 0)
            .ToList();
    }

    private static List<AirportRecord> PickDestinations(AirportRecord origin, List<AirportRecord> airports, int destinationsPerOrigin, Random random)
    {
        return airports
            .Where(airport => airport.Iata != origin.Iata)
            .OrderBy(_ => random.Next())
            .Take(destinationsPerOrigin)
            .ToList();
    }

    private static List<AirportRecord> PickHighTrafficOrigins(List<AirportRecord> airports, int maxCount, Random random)
    {
        var matches = new List<AirportRecord>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in PreferredHighTrafficOriginTerms)
        {
            var termMatches = airports
                .Where(airport => !usedCodes.Contains(airport.Iata) && MatchesHighTrafficTerm(airport, term))
                .OrderBy(airport => airport.Iata.Length == term.Length && airport.Iata.Equals(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(airport => airport.DisplayCity.Length)
                .ThenBy(airport => airport.Name.Length)
                .Take(maxCount - matches.Count)
                .ToList();

            foreach (var airport in termMatches)
            {
                matches.Add(airport);
                usedCodes.Add(airport.Iata);
            }

            if (matches.Count >= maxCount)
            {
                break;
            }
        }

        return matches
            .OrderBy(_ => random.Next())
            .ToList();
    }

    private static bool MatchesHighTrafficTerm(AirportRecord airport, string term)
    {
        return ContainsTerm(airport.DisplayCity, term)
            || ContainsTerm(airport.Name, term)
            || ContainsTerm(airport.Country, term)
            || airport.Iata.Equals(term, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(airport.Icao) && airport.Icao.Equals(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsTerm(string? value, string term) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool IsGuaranteedDailyOrigin(AirportRecord airport) =>
        airport.Country.Equals("Israel", StringComparison.OrdinalIgnoreCase);

    private static DateTime BuildDeparture(DateTime date, int flightIndex, int dailyFlights, Random random)
    {
        var serviceWindowStart = 5 * 60;
        var serviceWindowMinutes = 18 * 60;
        var slotMinutes = Math.Max(10, serviceWindowMinutes / Math.Max(1, dailyFlights));
        var minuteOfDay = serviceWindowStart + flightIndex * slotMinutes + random.Next(Math.Max(1, Math.Min(slotMinutes, 20)));
        return DateTime.SpecifyKind(date.AddMinutes(Math.Min(minuteOfDay, 23 * 60 + 45)), DateTimeKind.Utc);
    }

    private static async Task<int> SaveScheduleBatchAsync(
        AirportDbContext dbContext,
        IConfiguration configuration,
        List<GeneratedFlightSchedule> schedules,
        int alreadySaved,
        CancellationToken cancellationToken)
    {
        var count = schedules.Count;
        dbContext.GeneratedFlightSchedules.AddRange(schedules);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        schedules.Clear();
        LogSeed(configuration, $"Saved generated flight batch. Total saved so far: {alreadySaved + count}.");
        return count;
    }

    private static AircraftRecord? FindAvailableAircraft(IReadOnlyList<AircraftRecord> aircraft, Dictionary<string, DateTime> nextAvailable, DateTime departure, Random random) =>
        aircraft.Where(item => nextAvailable[item.Registration] <= departure).OrderBy(_ => random.Next()).FirstOrDefault();

    private static int FlightDurationMinutes(int distanceKm, Random random) =>
        Math.Clamp(distanceKm / 13 + random.Next(35, 75), 45, 900);

    private static string BuildFlightNumber(string originCode, Random random)
    {
        var code = new string(originCode.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        return $"{(string.IsNullOrWhiteSpace(code) ? "FL" : code)}{random.Next(100, 9999)}";
    }

    private static string BuildBoardingGate(AirportRecord origin, Random random) =>
        $"{origin.Iata}-T{random.Next(1, 6)}-G{random.Next(1, 31):D2}";

    private static decimal BuildBasePrice(int distanceKm) =>
        Math.Round(Math.Clamp(90m + distanceKm * 0.08m, 120m, 950m), 2);

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
        return roll < 0.02 ? FlightStatus.Cancelled : roll < 0.09 ? FlightStatus.Delayed : FlightStatus.Scheduled;
    }

    private static int DistanceKm(AirportRecord origin, AirportRecord destination)
    {
        if (!origin.Latitude.HasValue || !origin.Longitude.HasValue || !destination.Latitude.HasValue || !destination.Longitude.HasValue)
        {
            return Math.Abs(HashCode.Combine(origin.Iata, destination.Iata)) % 9000 + 350;
        }

        const double earthRadiusKm = 6371;
        var originLat = ToRadians(origin.Latitude.Value);
        var destinationLat = ToRadians(destination.Latitude.Value);
        var latDelta = ToRadians(destination.Latitude.Value - origin.Latitude.Value);
        var lonDelta = ToRadians(destination.Longitude.Value - origin.Longitude.Value);
        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2)
            + Math.Cos(originLat) * Math.Cos(destinationLat) * Math.Sin(lonDelta / 2) * Math.Sin(lonDelta / 2);
        return Math.Max(1, (int)Math.Round(earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a))));
    }

    private static double ToRadians(double value) => value * Math.PI / 180;
    private static int ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static double? ParseDouble(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static string Clean(string? value) => (value ?? string.Empty).Trim() is var cleaned && cleaned is @"\N" or "N/A" ? string.Empty : cleaned;

    private static string First(IReadOnlyDictionary<string, string> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static string? LimitOrNull(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : Limit(value, maxLength);

    private static void LogSeed(IConfiguration configuration, string message)
    {
        var configuredPath = configuration["AIRPORT_SEED_LOG_PATH"];
        var logPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "logs", "seed.log")
            : Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? AppContext.BaseDirectory);
        File.AppendAllText(logPath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path, bool hasHeader)
    {
        using var reader = new StreamReader(path);
        var headers = hasHeader ? ParseCsvLine(reader.ReadLine() ?? string.Empty) : [];

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
                row[index.ToString(CultureInfo.InvariantCulture)] = values[index];
                if (hasHeader && index < headers.Count)
                {
                    row[headers[index]] = values[index];
                }
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

    private sealed record AirportRecord(string Id, string Name, string Municipality, string Country, string Iata, string? Icao, double? Latitude, double? Longitude)
    {
        public string DisplayCity => !string.IsNullOrWhiteSpace(Municipality) ? Municipality : Name;
    }

    private sealed record AircraftRecord(string Registration, string Manufacturer, string Model, int SeatCapacity, int ManufactureYear);
}
