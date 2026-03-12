using System.Collections.Concurrent;
using System.Threading.Channels;

namespace SseNotificationApi.Services;

public sealed record SseEvent(string EventType, string Data);

public sealed class NotificationChannel
{
    private readonly ConcurrentDictionary<Guid, Channel<SseEvent>> _clients = new();

    public ChannelReader<SseEvent> RegisterClient(Guid userId)
    {
        var channel = Channel.CreateUnbounded<SseEvent>();
        _clients[userId] = channel;
        return channel.Reader;
    }

    public bool HasClient(Guid userId) => _clients.ContainsKey(userId);

    public void UnregisterClient(Guid userId)
    {
        if (_clients.TryRemove(userId, out var channel))
            channel.Writer.TryComplete();
    }

    public async Task<bool> PublishAsync(Guid targetUserId, string message, CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(targetUserId, out var clientChannel))
            return false;

        try
        {
            await clientChannel.Writer.WriteAsync(new SseEvent("notification", message), cancellationToken);
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
        var ev = new SseEvent("notification", message);

        foreach (var (id, channel) in _clients)
        {
            try
            {
                await channel.Writer.WriteAsync(ev, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                failed.Add(id);
            }
        }

        foreach (var id in failed)
            UnregisterClient(id);
    }

    public async Task PublishUserListAsync(string usersJson, CancellationToken cancellationToken = default)
    {
        var failed = new List<Guid>();
        var ev = new SseEvent("user_list", usersJson);

        foreach (var (id, channel) in _clients)
        {
            try
            {
                await channel.Writer.WriteAsync(ev, cancellationToken);
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