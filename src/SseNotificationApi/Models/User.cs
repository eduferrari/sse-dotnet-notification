using System.Text.Json.Serialization;

namespace SseNotificationApi.Models;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Offline;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
}

public enum UserStatus { Offline = 0, Online = 1 }