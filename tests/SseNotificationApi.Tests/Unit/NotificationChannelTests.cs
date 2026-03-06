using SseNotificationApi.Services;

namespace SseNotificationApi.Tests.Unit;

public sealed class NotificationChannelTests
{
    private readonly NotificationChannel _channel = new();

    [Fact]
    public void RegisterClient_ReturnsChannelReader()
    {
        var reader = _channel.RegisterClient(Guid.NewGuid());

        Assert.NotNull(reader);
    }

    [Fact]
    public void UnregisterClient_CompletesTheChannel()
    {
        var id = Guid.NewGuid();
        var reader = _channel.RegisterClient(id);

        _channel.UnregisterClient(id);

        Assert.True(reader.Completion.IsCompleted);
    }

    [Fact]
    public void UnregisterClient_DoesNotThrow_WhenClientDoesNotExist()
    {
        var exception = Record.Exception(() => _channel.UnregisterClient(Guid.NewGuid()));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_ReturnsFalse_WhenClientNotRegistered()
    {
        var result = await _channel.PublishAsync(Guid.NewGuid(), "msg", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task PublishAsync_ReturnsTrue_AndDeliversMessage()
    {
        var id = Guid.NewGuid();
        var reader = _channel.RegisterClient(id);

        var result = await _channel.PublishAsync(id, "hello", CancellationToken.None);

        Assert.True(result);
        Assert.True(reader.TryRead(out var received));
        Assert.Equal("hello", received);
    }

    [Fact]
    public async Task PublishToAllAsync_DeliversToAllRegisteredClients()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var reader1 = _channel.RegisterClient(id1);
        var reader2 = _channel.RegisterClient(id2);

        await _channel.PublishToAllAsync("broadcast", CancellationToken.None);

        Assert.True(reader1.TryRead(out var msg1));
        Assert.True(reader2.TryRead(out var msg2));
        Assert.Equal("broadcast", msg1);
        Assert.Equal("broadcast", msg2);
    }

    [Fact]
    public async Task PublishToAllAsync_DoesNotThrow_WhenNoClientsRegistered()
    {
        var exception = await Record.ExceptionAsync(() =>
            _channel.PublishToAllAsync("msg", CancellationToken.None));

        Assert.Null(exception);
    }
}