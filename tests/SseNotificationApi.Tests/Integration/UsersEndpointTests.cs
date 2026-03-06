using System.Net;
using System.Net.Http.Json;
using SseNotificationApi.Models;
using SseNotificationApi.Tests.Infrastructure;

namespace SseNotificationApi.Tests.Integration;

public sealed class UsersEndpointTests(WebAppFactory factory)
    : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostUsers_CreatesNewUser_AndReturnsOk()
    {
        var res = await _client.PostAsJsonAsync("/api/users", new { username = "Alice" });

        res.EnsureSuccessStatusCode();
        var user = await res.Content.ReadFromJsonAsync<User>();
        Assert.NotNull(user);
        Assert.Equal("Alice", user.Username);
        Assert.Equal(UserStatus.Offline, user.Status);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public async Task PostUsers_ReturnsSameUser_WhenUsernameAlreadyExists()
    {
        await _client.PostAsJsonAsync("/api/users", new { username = "Bob" });
        var res = await _client.PostAsJsonAsync("/api/users", new { username = "Bob" });

        res.EnsureSuccessStatusCode();
        var users = await _client.GetFromJsonAsync<User[]>("/api/users");
        Assert.Single(users!.Where(u => u.Username == "Bob"));
    }

    [Fact]
    public async Task PostUsers_ReturnsBadRequest_WhenUsernameIsEmpty()
    {
        var res = await _client.PostAsJsonAsync("/api/users", new { username = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PostUsers_ReturnsBadRequest_WhenUsernameIsWhitespace()
    {
        var res = await _client.PostAsJsonAsync("/api/users", new { username = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllCreatedUsers()
    {
        await _client.PostAsJsonAsync("/api/users", new { username = "Charlie" });

        var users = await _client.GetFromJsonAsync<User[]>("/api/users");

        Assert.NotNull(users);
        Assert.Contains(users, u => u.Username == "Charlie");
    }
}