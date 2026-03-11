using System.Text.Json;
using Confluent.Kafka;

namespace SseNotificationApi.Services;

public interface IKafkaProducerService
{
    Task PublishAsync(Guid? targetUserId, string message, CancellationToken cancellationToken);
}

public sealed record KafkaNotificationMessage(Guid? TargetUserId, string Message);

public sealed class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaProducerService(IConfiguration configuration)
    {
        _topic = configuration["Kafka:Topic"]!;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
        }).Build();
    }

    public async Task PublishAsync(Guid? targetUserId, string message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new KafkaNotificationMessage(targetUserId, message));
        var key = targetUserId?.ToString() ?? "broadcast";

        await _producer.ProduceAsync(_topic,
            new Message<string, string> { Key = key, Value = payload },
            cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}