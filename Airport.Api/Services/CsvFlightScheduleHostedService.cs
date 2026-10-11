using Airport.Api.Data;

namespace Airport.Api.Services;

public class CsvFlightScheduleHostedService(IServiceProvider services, ILogger<CsvFlightScheduleHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AirportDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            await CsvFlightScheduleSeeder.SeedAsync(dbContext, configuration, environment, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            logger.LogError(error, "CSV flight schedule seeding failed.");
        }
    }
}
