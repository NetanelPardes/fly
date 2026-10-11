using Airport.Api.Data;
using Airport.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Airport.Api.Services;

public static class DemoDataSeeder
{
    public static async Task SeedUsersAsync(IServiceProvider services)
    {
        const int maxAttempts = 20;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AirportDbContext>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

                await dbContext.Database.MigrateAsync();

                if (configuration.GetValue("AIRPORT_SEED_DEMO_FLIGHTS", true))
                {
                    await SeedDemoFlightsAsync(dbContext);
                }

                if (await dbContext.Users.AnyAsync())
                {
                    return;
                }

                var users = new[]
                {
                    new User { FullName = "Demo Admin", Email = "admin@airport.local", Role = UserRole.Admin },
                    new User { FullName = "Demo Ticketing", Email = "ticketing@airport.local", Role = UserRole.Ticketing },
                    new User { FullName = "Demo Baggage", Email = "baggage@airport.local", Role = UserRole.Baggage },
                    new User { FullName = "Demo Passenger", Email = "passenger@airport.local", Role = UserRole.Passenger }
                };

                foreach (var user in users)
                {
                    user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");
                    user.CreatedAt = DateTime.UtcNow;
                }

                dbContext.Users.AddRange(users);
                await dbContext.SaveChangesAsync();
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    private static async Task SeedDemoFlightsAsync(AirportDbContext dbContext)
    {
        const string source = "DemoSchedule";
        if (await dbContext.Flights.AnyAsync(flight => flight.Source == source))
        {
            return;
        }

        var routes = new[]
        {
            ("Tel Aviv", "London"),
            ("London", "Tel Aviv"),
            ("Tel Aviv", "Paris"),
            ("Paris", "Tel Aviv"),
            ("Tel Aviv", "Athens"),
            ("Athens", "Tel Aviv"),
            ("Rome", "Tel Aviv"),
            ("Tel Aviv", "Dubai")
        };

        var start = DateTime.UtcNow.Date.AddMonths(-6);
        var flights = new List<Flight>();
        var sequence = 300;

        for (var day = 0; day <= 365; day += 2)
        {
            var date = start.AddDays(day);
            for (var routeIndex = 0; routeIndex < routes.Length; routeIndex++)
            {
                var route = routes[routeIndex];
                var departure = date.AddHours(6 + routeIndex * 2);
                var status = departure < DateTime.UtcNow.AddDays(-2)
                    ? FlightStatus.Departed
                    : departure < DateTime.UtcNow
                        ? FlightStatus.Boarding
                        : day % 19 == 0
                            ? FlightStatus.Delayed
                            : FlightStatus.Scheduled;

                flights.Add(new Flight
                {
                    FlightNumber = $"AM{sequence++}",
                    Origin = route.Item1,
                    Destination = route.Item2,
                    DepartureTime = departure,
                    ArrivalTime = departure.AddHours(3).AddMinutes(routeIndex * 8),
                    Status = status,
                    GateId = routeIndex % 3 + 1,
                    ExternalId = $"demo-schedule-{date:yyyyMMdd}-{routeIndex}",
                    Source = source,
                    LastUpdatedAt = DateTime.UtcNow
                });
            }
        }

        dbContext.Flights.AddRange(flights);
        await dbContext.SaveChangesAsync();
    }
}
