using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SseNotificationApi.Data;
using SseNotificationApi.Services;

namespace SseNotificationApi.Tests.Infrastructure;

public sealed class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public WebAppFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Substituir Postgres por SQLite in-memory nos testes
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // Remover o KafkaConsumerService para não tentar conectar a um broker real
            var consumerDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(KafkaConsumerService));
            if (consumerDescriptor is not null)
                services.Remove(consumerDescriptor);

            // Substituir o producer por uma implementação no-op
            var producerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IKafkaProducerService));
            if (producerDescriptor is not null)
                services.Remove(producerDescriptor);

            services.AddSingleton<IKafkaProducerService, NoOpKafkaProducerService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

file sealed class NoOpKafkaProducerService : IKafkaProducerService
{
    public Task PublishAsync(Guid? targetUserId, string message, CancellationToken cancellationToken)
        => Task.CompletedTask;
}