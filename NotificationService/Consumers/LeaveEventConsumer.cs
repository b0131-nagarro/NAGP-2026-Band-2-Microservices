using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Consumers;

/// <summary>
/// Background service that consumes leave events from RabbitMQ and logs
/// human-readable notification messages.
///
/// Per the assignment spec: no real email/SMS – all notifications are log entries.
/// Uses RabbitMQ.Client v7 async API.
/// </summary>
public sealed class LeaveEventConsumer : BackgroundService
{
    private readonly ILogger<LeaveEventConsumer> _logger;
    private readonly IConfiguration _config;

    private const string Exchange  = "leave.events";
    private const string QueueName = "notification.leave.queue";
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private static readonly ActivitySource ActivitySource = new("notification-service");

    public LeaveEventConsumer(ILogger<LeaveEventConsumer> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait for RabbitMQ to be ready before connecting
        await WaitForRabbitAsync(ct);
        if (ct.IsCancellationRequested) return;

        var factory = new ConnectionFactory
        {
            HostName                 = _config["RabbitMQ:Host"]     ?? "localhost",
            UserName                 = _config["RabbitMQ:Username"] ?? "guest",
            Password                 = _config["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
        };

        await using var connection = await factory.CreateConnectionAsync("notification-consumer", ct);
        await using var channel    = await connection.CreateChannelAsync(cancellationToken: ct);

        // Declare exchange + queue (idempotent – matches publisher declarations)
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(QueueName,
            durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        await channel.QueueBindAsync(QueueName, Exchange, routingKey: string.Empty, cancellationToken: ct);

        // Process one message at a time (fair dispatch)
        await channel.BasicQosAsync(0, prefetchCount: 1, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var parentContext = Propagator.Extract(
                default,
                ea.BasicProperties.Headers,
                static (headers, key) =>
                {
                    if (headers is null || !headers.TryGetValue(key, out var value))
                        return null;

                    return value switch
                    {
                        byte[] bytes => new[] { Encoding.UTF8.GetString(bytes) },
                        string s     => new[] { s },
                        _            => value?.ToString() is { } str ? new[] { str } : null
                    };
                });

            using var activity = ActivitySource.StartActivity(
                "ProcessLeaveEvent",
                ActivityKind.Consumer,
                parentContext.ActivityContext);

            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                ProcessEvent(json);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification event");
                // Nack without requeue to avoid poison-message loops
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);
        _logger.LogInformation("Notification consumer listening on queue: {Queue}", QueueName);

        // Keep alive until the host requests shutdown
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
    }

    // ── Event dispatching ─────────────────────────────────────────────────────

    private void ProcessEvent(string json)
    {
        var doc       = JsonNode.Parse(json);
        if (doc is null) return;

        var eventType = doc["EventType"]?.GetValue<string>() ?? "Unknown";
        var payload   = doc["Payload"];

        switch (eventType)
        {
            case "LeaveApplied":  LogApplied(payload);  break;
            case "LeaveApproved": LogApproved(payload); break;
            case "LeaveRejected": LogRejected(payload); break;
            default:
                _logger.LogWarning("Unknown event type received: {EventType}", eventType);
                break;
        }
    }

    private void LogApplied(JsonNode? p)
    {
        var empId  = p?["EmployeeId"]?.GetValue<string>()   ?? "-";
        var name   = p?["EmployeeName"]?.GetValue<string>()  ?? "Unknown";
        var type   = p?["LeaveType"]?.GetValue<string>()    ?? "-";
        var days   = p?["NumberOfDays"]?.GetValue<int>()    ?? 0;
        var start  = p?["StartDate"]?.GetValue<DateTime>()  ?? DateTime.MinValue;
        var end    = p?["EndDate"]?.GetValue<DateTime>()    ?? DateTime.MinValue;
        var mgrId  = p?["ManagerId"]?.GetValue<string>()   ?? "-";

        _logger.LogInformation(
            "[NOTIFICATION → EMPLOYEE {EmpId}] Your {Type} leave request for {Days} day(s) " +
            "({Start:yyyy-MM-dd} to {End:yyyy-MM-dd}) is now PENDING approval.",
            empId, type, days, start, end);

        _logger.LogInformation(
            "[NOTIFICATION → MANAGER {MgrId}] {Name} applied for {Days} day(s) of {Type} leave " +
            "({Start:yyyy-MM-dd} to {End:yyyy-MM-dd}). Please review.",
            mgrId, name, days, type, start, end);
    }

    private void LogApproved(JsonNode? p)
    {
        var empId = p?["EmployeeId"]?.GetValue<string>()   ?? "-";
        var name  = p?["EmployeeName"]?.GetValue<string>()  ?? "Unknown";
        var type  = p?["LeaveType"]?.GetValue<string>()    ?? "-";
        var days  = p?["NumberOfDays"]?.GetValue<int>()    ?? 0;

        _logger.LogInformation(
            "[NOTIFICATION → EMPLOYEE {EmpId}] {Name} – your {Type} leave for {Days} day(s) " +
            "has been APPROVED. Your balance has been updated.",
            empId, name, type, days);
    }

    private void LogRejected(JsonNode? p)
    {
        var empId  = p?["EmployeeId"]?.GetValue<string>()      ?? "-";
        var name   = p?["EmployeeName"]?.GetValue<string>()     ?? "Unknown";
        var type   = p?["LeaveType"]?.GetValue<string>()       ?? "-";
        var reason = p?["RejectionReason"]?.GetValue<string>() ?? "No reason provided";

        _logger.LogInformation(
            "[NOTIFICATION → EMPLOYEE {EmpId}] {Name} – your {Type} leave request has been REJECTED. " +
            "Reason: {Reason}",
            empId, name, type, reason);
    }

    // ── Startup wait ──────────────────────────────────────────────────────────

    private async Task WaitForRabbitAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"]     ?? "localhost",
            UserName = _config["RabbitMQ:Username"] ?? "guest",
            Password = _config["RabbitMQ:Password"] ?? "guest"
        };

        for (int i = 1; i <= 15 && !ct.IsCancellationRequested; i++)
        {
            try
            {
                await using var conn = await factory.CreateConnectionAsync(ct);
                _logger.LogInformation("RabbitMQ reachable (attempt {Attempt})", i);
                return;
            }
            catch
            {
                _logger.LogWarning("RabbitMQ not ready (attempt {Attempt}/15) – retrying in 5s", i);
                await Task.Delay(5_000, ct);
            }
        }

        _logger.LogError("Could not connect to RabbitMQ after 15 attempts");
    }
}
