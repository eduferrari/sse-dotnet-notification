using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SseNotificationApi.Services;

public sealed class NotificationChannel
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    public (Guid ClientId, ChannelReader<string> Reader) RegisterClient()
    {
        var channel = Channel.CreateUnbounded<string>();
        var id = Guid.NewGuid();

        _clients[id] = channel;

        return (id, channel.Reader);
    }

    public void UnregisterClient(Guid clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public async Task PublishAsync(string message, CancellationToken cancellationToken)
    {
        var failedClients = new List<Guid>();

        foreach (var (clientId, clientChannel) in _clients)
        {
            try
            {
                await clientChannel.Writer.WriteAsync(message, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                failedClients.Add(clientId);
            }
        }

        foreach (var clientId in failedClients)
        {
            UnregisterClient(clientId);
        }
    }
}
