namespace NotificationService.Models;

/// <summary>
/// Envelope wrapping every event published by LeaveService.
/// The Payload is deserialized per-EventType.
/// </summary>
public record NotificationEnvelope(
    string   EventType,
    DateTime OccurredAt,
    object   Payload);

/// <summary>Logged notification record (in-memory only – no persistence per spec).</summary>
public record NotificationLog(
    Guid     Id,
    string   EventType,
    string   Recipient,
    string   Message,
    DateTime LoggedAt);
