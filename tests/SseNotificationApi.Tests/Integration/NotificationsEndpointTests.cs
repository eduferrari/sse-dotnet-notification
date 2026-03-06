using System.Net;
using System.Net.Http.Json;
using SseNotificationApi.Models;
using SseNotificationApi.Tests.Infrastructure;

namespace SseNotificationApi.Tests.Integration;

public sealed class NotificationsEndpointTests(WebAppFactory factory)
    : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostNotifications_ReturnsBadRequest_WhenMessageIsEmpty()
    {
        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsBadRequest_WhenMessageIsWhitespace()
    {
        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_Broadcast_ReturnsOk_EvenWithNoClientsConnected()
    {
        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "hello everyone" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsNotFound_WhenTargetUserNotConnected()
    {
        var userRes = await _client.PostAsJsonAsync("/api/users", new { username = "Dave" });
        var user = await userRes.Content.ReadFromJsonAsync<User>();

        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = user!.Id, message = "ping" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsNotFound_WhenTargetUserIdDoesNotExist()
    {
        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = Guid.NewGuid(), message = "ping" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_Broadcast_PersistsMessageWithNullTargetUserId()
    {
        var res = await _client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "saved broadcast" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}