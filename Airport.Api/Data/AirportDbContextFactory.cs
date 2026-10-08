using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Airport.Api.Data;

public class AirportDbContextFactory : IDesignTimeDbContextFactory<AirportDbContext>
{
    public AirportDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AirportDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=airport_management;User=airport_user;Password=airport_password;TreatTinyAsBoolean=true;";

        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
        return new AirportDbContext(optionsBuilder.Options);
    }
}
