using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SseNotificationApi.Services;

public sealed class NotificationChannel
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();

    public ChannelReader<string> RegisterClient(Guid userId)
    {
        var channel = Channel.CreateUnbounded<string>();
        _clients[userId] = channel;
        return channel.Reader;
    }

    public bool HasClient(Guid userId) => _clients.ContainsKey(userId);

    public void UnregisterClient(Guid userId)
    {
        if (_clients.TryRemove(userId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public async Task<bool> PublishAsync(Guid targetUserId, string message, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(targetUserId, out var clientChannel))
            return false;

        try
        {
            await clientChannel.Writer.WriteAsync(message, cancellationToken);
            return true;
        }
        catch (ChannelClosedException)
        {
            UnregisterClient(targetUserId);
            return false;
        }
    }

    public async Task PublishToAllAsync(string message, CancellationToken cancellationToken)
    {
        var failed = new List<Guid>();

        foreach (var (id, channel) in _clients)
        {
            try
            {
                await channel.Writer.WriteAsync(message, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                failed.Add(id);
            }
        }

        foreach (var id in failed)
            UnregisterClient(id);
    }
}