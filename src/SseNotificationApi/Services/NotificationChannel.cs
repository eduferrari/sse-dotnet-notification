using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SseNotificationApi.Services;

public sealed class NotificationChannel
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    public ChannelReader<string> RegisterClient(Guid clientId)
    {
        var channel = Channel.CreateUnbounded<string>();
        _clients[clientId] = channel;
        return channel.Reader;
    }

    public void UnregisterClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public async Task<bool> PublishAsync(Guid targetClientId, string message, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(targetClientId, out var clientChannel))
        {
            return false;
        }

        try
        {
            await clientChannel.Writer.WriteAsync(message, cancellationToken);
            return true;
        }
        catch (ChannelClosedException)
        {
            UnregisterClient(targetClientId);
            return false;
        }
    }
}
