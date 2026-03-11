using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace SseNotificationApi.Services;

public sealed class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly NotificationChannel _channel;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string _bootstrapServers;
    private readonly string _topic;

    public KafkaConsumerService(
        IConfiguration configuration,
        NotificationChannel channel,
        ILogger<KafkaConsumerService> logger)
    {
        _channel = channel;
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"]!;
        _topic = configuration["Kafka:Topic"]!;

        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        }).Build();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = _bootstrapServers }).Build();

        try
        {
            await adminClient.CreateTopicsAsync([new TopicSpecification
            {
                Name = _topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }]);
            _logger.LogInformation("Tópico Kafka '{Topic}' criado.", _topic);
        }
        catch (CreateTopicsException ex)
            when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // tópico já existe, tudo certo
        }

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            _consumer.Subscribe(_topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = _consumer.Consume(stoppingToken);
                        if (result?.Message?.Value is null) continue;

                        var notification = JsonSerializer.Deserialize<KafkaNotificationMessage>(result.Message.Value);
                        if (notification is null) continue;

                        if (notification.TargetUserId is null)
                            await _channel.PublishToAllAsync(notification.Message, stoppingToken);
                        else
                            await _channel.PublishAsync(notification.TargetUserId.Value, notification.Message, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar mensagem do Kafka");
                    }
                }
            }
            finally
            {
                _consumer.Close();
            }
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}