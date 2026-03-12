namespace SseNotificationApi.Models;

// TargetUserId = null → broadcast para todos os usuários online
public sealed record NotificationRequest(Guid? TargetUserId, string Message);

public sealed record AuthRequest(string Username, string Password);