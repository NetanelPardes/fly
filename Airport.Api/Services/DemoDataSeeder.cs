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
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

                await dbContext.Database.MigrateAsync();

                var passengersAlreadySeeded = await dbContext.Users.AnyAsync(user => user.Role == UserRole.Passenger);
                var employeesAlreadySeeded = await dbContext.Employees.AnyAsync();
                if (passengersAlreadySeeded && employeesAlreadySeeded)
                {
                    return;
                }

                foreach (var user in BuildSeedPassengers())
                {
                    var exists = await dbContext.Users.AnyAsync(existing =>
                        existing.Email == user.Email
                        || existing.NationalId == user.NationalId
                        || existing.PhoneNumber == user.PhoneNumber);
                    if (exists)
                    {
                        continue;
                    }

                    user.PasswordHash = passwordHasher.HashPassword(user, "1234");
                    user.CreatedAt = DateTime.UtcNow;
                    dbContext.Users.Add(user);
                }

                foreach (var employee in BuildSeedEmployees())
                {
                    var exists = await dbContext.Employees.AnyAsync(existing =>
                        existing.Email == employee.Email
                        || existing.NationalId == employee.NationalId
                        || existing.PhoneNumber == employee.PhoneNumber);
                    if (exists)
                    {
                        continue;
                    }

                    employee.PasswordHash = passwordHasher.HashPassword(ToPasswordUser(employee), "1234");
                    employee.CreatedAt = DateTime.UtcNow;
                    dbContext.Employees.Add(employee);
                }

                await dbContext.SaveChangesAsync();
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    private static IEnumerable<User> BuildSeedPassengers()
    {
        var passengers = new[]
        {
            "Altman Elazar",
            "Elmalich Avraham",
            "Bagad Pesach",
            "Ben Chaim Yedidya",
            "Bar-Tal Mordechai",
            "Gross Avraham",
            "Gershi Elazar",
            "Yosilevitz Yoel",
            "Cohen Menachem",
            "Levental Yoni",
            "Pirov Asher",
            "Pesach Mordechai",
            "Pardes Netanel",
            "Kanner Ariel",
            "Katzburg Aharon",
            "Rosengarten Avraham",
            "Ruchlin Meir",
            "Reiner Yitzchak",
            "Shuv Shlomo",
            "Shteren David",
            "Netanel Kaplin"
        };

        var sequence = 1;
        foreach (var name in passengers)
        {
            yield return BuildUser(name, UserRole.Passenger, sequence++);
        }
    }

    private static IEnumerable<Employee> BuildSeedEmployees()
    {
        var admins = new[]
        {
            "Moishy Horowitz",
            "Netanel Amar",
            "Noam Hadad",
            "Yaniv Hadar"
        };

        var sequence = 1;
        foreach (var name in admins)
        {
            yield return BuildEmployee(name, sequence++);
        }
    }

    private static User BuildUser(string fullName, UserRole role, int sequence)
    {
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = parts.Length > 0 ? parts[0] : fullName;
        var lastName = parts.Length > 1 ? parts[1] : "User";
        var email = $"{NormalizeEmailPart(firstName)}.{NormalizeEmailPart(lastName)}@gmail.com";

        return new User
        {
            NationalId = (100000000 + sequence * 7919).ToString(),
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}",
            Email = email,
            Role = role,
            Gender = "m",
            Address = $"{10 + sequence} Airport Avenue",
            PhoneNumber = $"050{sequence:0000000}"
        };
    }

    private static string NormalizeEmailPart(string value) =>
        new(value.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character)).ToArray());

    private static Employee BuildEmployee(string fullName, int sequence)
    {
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = parts.Length > 0 ? parts[0] : fullName;
        var lastName = parts.Length > 1 ? parts[1] : "Manager";
        var email = $"{NormalizeEmailPart(firstName)}.{NormalizeEmailPart(lastName)}@gmail.com";

        return new Employee
        {
            NationalId = (200000000 + sequence * 7919).ToString(),
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}",
            Email = email,
            Gender = "m",
            Address = $"{50 + sequence} Airport Avenue",
            PhoneNumber = $"052{sequence:0000000}",
            Rank = "Manager"
        };
    }

    private static User ToPasswordUser(Employee employee) => new()
    {
        Id = employee.Id,
        NationalId = employee.NationalId,
        FirstName = employee.FirstName,
        LastName = employee.LastName,
        FullName = employee.FullName,
        Email = employee.Email,
        Role = UserRole.Admin,
        Gender = employee.Gender,
        Address = employee.Address,
        PhoneNumber = employee.PhoneNumber
    };
}
