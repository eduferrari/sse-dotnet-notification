using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SseNotificationApi.Models;
using SseNotificationApi.Tests.Infrastructure;

namespace SseNotificationApi.Tests.Integration;

public sealed class UsersEndpointTests(WebAppFactory factory)
    : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task Register_CreatesNewUser_AndReturnsToken()
    {
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { username = "Alice", password = "Pass@1" });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        Assert.Equal("Alice", body.GetProperty("username").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUsernameAlreadyExists()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { username = "Bob_dup", password = "Pass@1" });

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { username = "Bob_dup", password = "Pass@1" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenUsernameIsEmpty()
    {
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { username = "", password = "Pass@1" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordIsEmpty()
    {
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { username = "Alice2", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsToken_WithValidCredentials()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { username = "Carol", password = "Pass@1" });

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "Carol", password = "Pass@1" });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WithWrongPassword()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new { username = "Dan", password = "CorrectPass@1" });

        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "Dan", password = "WrongPass@1" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllCreatedUsers_WhenAuthenticated()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Eve_list");

        var users = await client.GetFromJsonAsync<User[]>("/api/users");

        Assert.NotNull(users);
        Assert.Contains(users, u => u.Username == "Eve_list");
    }

    [Fact]
    public async Task GetUsers_ReturnsUnauthorized_WithoutToken()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task GetUsers_DoesNotExposePasswordHash()
    {
        var (client, _, _) = await factory.CreateAuthenticatedClientAsync("Frank_nohash");

        var res  = await client.GetAsync("/api/users");
        var json = await res.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
    }
}
