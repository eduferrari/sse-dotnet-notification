namespace SseNotificationApi.Models;

public sealed class Message
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? TargetUserId { get; init; }   // null = broadcast
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}