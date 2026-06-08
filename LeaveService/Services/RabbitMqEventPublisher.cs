using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace LeaveService.Services;

public interface IEventPublisher
{
    Task PublishAsync(string eventType, object payload);
}

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    private const string Exchange = "leave.events";

    private RabbitMqEventPublisher(IConnection connection, IChannel channel, ILogger<RabbitMqEventPublisher> logger)
    {
        _connection = connection;
        _channel    = channel;
        _logger     = logger;
    }

    public static async Task<RabbitMqEventPublisher> CreateAsync(
        IConfiguration config,
        ILogger<RabbitMqEventPublisher> logger)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"]     ?? "localhost",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true
        };

        var connection = await factory.CreateConnectionAsync("leave-service-publisher");
        var channel    = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);

        logger.LogInformation("RabbitMQ publisher ready on {Exchange}", Exchange);
        return new RabbitMqEventPublisher(connection, channel, logger);
    }

    public async Task PublishAsync(string eventType, object payload)
    {
        var envelope = new
        {
            EventType  = eventType,
            OccurredAt = DateTime.UtcNow,
            Payload    = payload
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));

        var props = new BasicProperties
        {
            Persistent  = true,
            ContentType = "application/json",
            Headers     = new Dictionary<string, object?>
            {
                ["event-type"] = eventType,
                ["source"]     = "leave-service"
            }
        };

        await _channel.BasicPublishAsync(Exchange, string.Empty, false, props, body);

        _logger.LogInformation("Published {EventType}", eventType);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}
