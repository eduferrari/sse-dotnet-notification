using System.Net;
using System.Net.Http.Json;
using SseNotificationApi.Tests.Infrastructure;

namespace SseNotificationApi.Tests.Integration;

public sealed class NotificationsEndpointTests(WebAppFactory factory)
    : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task PostNotifications_ReturnsBadRequest_WhenMessageIsEmpty()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_empty");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsBadRequest_WhenMessageIsWhitespace()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_ws");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_Broadcast_ReturnsOk_EvenWithNoClientsConnected()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_broadcast");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "hello everyone" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsNotFound_WhenTargetUserNotConnected()
    {
        var (client, _, _)  = await factory.CreateAuthenticatedClientAsync("Notif_sender");
        var (client2, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_target_offline");

        // Register a second user but don't open a SSE stream (not connected)
        var usersRes = await client.GetFromJsonAsync<dynamic[]>("/api/users");
        var users    = await client.GetFromJsonAsync<SseNotificationApi.Models.User[]>("/api/users");
        var target   = users!.First(u => u.Username == "Notif_target_offline");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = target.Id, message = "ping" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsNotFound_WhenTargetUserIdDoesNotExist()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_ghost");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = Guid.NewGuid(), message = "ping" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_Broadcast_PersistsMessageWithNullTargetUserId()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Notif_persist");

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "saved broadcast" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task PostNotifications_ReturnsUnauthorized_WithoutToken()
    {
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/notifications",
            new { targetUserId = (Guid?)null, message = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
