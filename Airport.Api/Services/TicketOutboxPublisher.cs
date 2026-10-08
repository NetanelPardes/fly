using Airport.Api.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;

namespace Airport.Api.Services;

public class TicketOutboxPublisher(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TicketOutboxPublisher> logger) : BackgroundService
{
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ticket outbox publish cycle failed");
            }

            await Task.Delay(_delay, stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AirportDbContext>();
        var messages = await dbContext.OutboxMessages
            .Where(message => message.PublishedAt == null && message.Attempts < 10)
            .OrderBy(message => message.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOST"] ?? "localhost",
            Port = int.TryParse(configuration["RABBITMQ_PORT"], out var port) ? port : 5672,
            UserName = configuration["RABBITMQ_USER"] ?? "guest",
            Password = configuration["RABBITMQ_PASSWORD"] ?? "guest",
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var queueName = configuration["RABBITMQ_TICKET_QUEUE"] ?? "ticket-generation";
        channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);

        foreach (var message in messages)
        {
            try
            {
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = message.EventId.ToString();
                properties.Type = message.Type;

                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: true,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.Payload));

                message.PublishedAt = DateTime.UtcNow;
                message.Attempts++;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox message {EventId}", message.EventId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
