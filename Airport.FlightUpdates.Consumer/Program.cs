using Airport.Api.Data;
using Airport.Api.Models;
using Airport.FlightUpdates.Consumer.Messages;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration["MYSQL_CONNECTION_STRING"] ?? BuildMySqlConnectionString(configuration);
var options = new DbContextOptionsBuilder<AirportDbContext>()
    .UseMySql(connectionString, new MySqlServerVersion(Version.Parse(configuration["MYSQL_SERVER_VERSION"] ?? "8.0.36")))
    .Options;

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "localhost:9092",
    GroupId = configuration["KAFKA_GROUP_ID"] ?? "airport-flight-updates",
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false
};

var topic = configuration["KAFKA_FLIGHT_TOPIC"] ?? "flight-updates";
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter());

using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
consumer.Subscribe(topic);

Console.WriteLine($"Listening for flight updates on {topic}...");

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    while (!cancellation.IsCancellationRequested)
    {
        var result = consumer.Consume(cancellation.Token);
        var update = JsonSerializer.Deserialize<FlightUpdateMessage>(result.Message.Value, jsonOptions);
        if (update is null)
        {
            Console.WriteLine("Skipped empty flight update message.");
            consumer.Commit(result);
            continue;
        }

        await using var dbContext = new AirportDbContext(options);
        await UpsertFlightAsync(dbContext, update, cancellation.Token);
        consumer.Commit(result);
        Console.WriteLine($"Persisted {update.Source}:{update.ExternalId} at offset {result.TopicPartitionOffset}.");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Consumer stopped.");
}
finally
{
    consumer.Close();
}

static async Task UpsertFlightAsync(AirportDbContext dbContext, FlightUpdateMessage update, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(update.Source) || string.IsNullOrWhiteSpace(update.ExternalId))
    {
        throw new InvalidOperationException("Source and ExternalId are required for flight de-duplication.");
    }

    var flight = await dbContext.Flights.FirstOrDefaultAsync(
        item => item.Source == update.Source && item.ExternalId == update.ExternalId,
        cancellationToken);

    if (flight is null)
    {
        flight = new Flight
        {
            Source = update.Source.Trim(),
            ExternalId = update.ExternalId.Trim()
        };
        dbContext.Flights.Add(flight);
    }

    flight.FlightNumber = update.FlightNumber.Trim();
    flight.Origin = update.Origin.Trim();
    flight.OriginAirportCode = NormalizeCode(update.OriginAirportCode, update.Origin);
    flight.Destination = update.Destination.Trim();
    flight.DestinationAirportCode = NormalizeCode(update.DestinationAirportCode, update.Destination);
    flight.BoardingGate = string.IsNullOrWhiteSpace(update.BoardingGate) ? string.Empty : update.BoardingGate.Trim();
    flight.AircraftCode = string.IsNullOrWhiteSpace(update.AircraftCode) ? string.Empty : update.AircraftCode.Trim();
    flight.DepartureTime = ToUtc(update.DepartureTime);
    flight.ArrivalTime = ToUtc(update.ArrivalTime);
    flight.Status = update.Status;
    flight.BasePrice = update.BasePrice.GetValueOrDefault(180m);

    await dbContext.SaveChangesAsync(cancellationToken);
}

static string NormalizeCode(string? value, string fallback)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        return value.Trim().ToUpperInvariant();
    }

    var letters = new string((fallback ?? string.Empty).Where(char.IsLetterOrDigit).Take(3).ToArray());
    return string.IsNullOrWhiteSpace(letters) ? "UNK" : letters.ToUpperInvariant();
}

static DateTime ToUtc(DateTime value)
{
    return value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

static string BuildMySqlConnectionString(IConfiguration configuration)
{
    var host = configuration["MYSQL_HOST"] ?? "localhost";
    var port = configuration["MYSQL_PORT"] ?? "3306";
    var database = configuration["MYSQL_DATABASE"] ?? "airport_management";
    var user = configuration["MYSQL_USER"] ?? "airport_user";
    var password = configuration["MYSQL_PASSWORD"] ?? "airport_password";

    return $"Server={host};Port={port};Database={database};User={user};Password={password};TreatTinyAsBoolean=true;";
}
