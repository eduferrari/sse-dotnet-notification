namespace SseNotificationApi.Models;

public sealed record NotificationRequest(Guid ClientId, string Message);
