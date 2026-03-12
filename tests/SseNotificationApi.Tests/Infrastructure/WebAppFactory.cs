using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SseNotificationApi.Data;
using SseNotificationApi.Services;

namespace SseNotificationApi.Tests.Infrastructure;

/// <summary>
/// Requires the dev Postgres from docker-compose to be running.
/// Uses a dedicated "ssenotification_test" database that is dropped and recreated before each test class.
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=ssenotification_test;Username=devuser;Password=devpass";

    public async Task InitializeAsync()
    {
        // Drop and recreate the test database to guarantee a clean state
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Key"]                            = "test-jwt-secret-key-for-tests-min-32-chars!!",
                ["Jwt:Issuer"]                         = "SseNotificationApi",
                ["Jwt:Audience"]                       = "SseNotificationApi",
                ["Jwt:ExpiresInMinutes"]               = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove KafkaConsumerService to avoid connecting to a real broker
            var consumerDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(KafkaConsumerService));
            if (consumerDescriptor is not null)
                services.Remove(consumerDescriptor);

            // Replace producer with no-op
            var producerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IKafkaProducerService));
            if (producerDescriptor is not null)
                services.Remove(producerDescriptor);

            services.AddSingleton<IKafkaProducerService, NoOpKafkaProducerService>();
        });
    }

    /// <summary>
    /// Registers a user and returns an authenticated HttpClient, JWT token, and userId.
    /// </summary>
    public async Task<(HttpClient Client, string Token, Guid UserId)> CreateAuthenticatedClientAsync(
        string username, string password = "Test@123!")
    {
        var client = CreateClient();
        var res    = await client.PostAsJsonAsync("/api/auth/register",
                         new { username, password });
        res.EnsureSuccessStatusCode();

        var body   = await res.Content.ReadFromJsonAsync<JsonElement>();
        var token  = body.GetProperty("token").GetString()!;
        var userId = body.GetProperty("userId").GetGuid();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (client, token, userId);
    }
}

file sealed class NoOpKafkaProducerService : IKafkaProducerService
{
    public Task PublishAsync(Guid? targetUserId, string message, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
