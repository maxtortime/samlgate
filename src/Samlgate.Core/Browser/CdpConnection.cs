using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Samlgate.Browser;

/// <summary>A DevTools Protocol event. SessionId is set for events from an attached target (flatten mode).</summary>
internal sealed record CdpEvent(string Method, JsonObject Params, string? SessionId);

/// <summary>
/// Minimal Chrome DevTools Protocol client over a single browser-level WebSocket, using flattened
/// target sessions. Only what samlgate needs: commands with replies, and an event stream.
/// </summary>
internal sealed class CdpConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly Channel<CdpEvent> _events = Channel.CreateUnbounded<CdpEvent>();
    private readonly CancellationTokenSource _receiveCts = new();
    private Task? _receiveLoop;
    private int _nextId;
    private volatile CdpClosedException? _closed;

    public ChannelReader<CdpEvent> Events => _events.Reader;

    public static async Task<CdpConnection> ConnectAsync(Uri webSocketUrl, CancellationToken cancellationToken)
    {
        var connection = new CdpConnection();
        connection._socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        await connection._socket.ConnectAsync(webSocketUrl, cancellationToken);
        connection._receiveLoop = Task.Run(connection.ReceiveLoopAsync);
        return connection;
    }

    public async Task<JsonObject> SendAsync(
        string method, JsonObject? parameters = null, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() };
        if (sessionId is not null)
        {
            message["sessionId"] = sessionId;
        }

        var reply = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;
        // The receive loop fails pending commands when it ends; a command registered after that must fail too
        if (_closed is { } closedBefore)
        {
            _pending.TryRemove(id, out _);
            throw closedBefore;
        }

        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException)
        {
            _pending.TryRemove(id, out _);
            throw new CdpClosedException(e);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }

        await using var registration = cancellationToken.Register(() => reply.TrySetCanceled(cancellationToken));
        return await reply.Task;
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        Exception? failure = null;

        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, _receiveCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                try
                {
                    Dispatch(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                }
                catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException)
                {
                    // Ignore a message we cannot understand rather than stop listening
                }

                message.SetLength(0);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException)
        {
            failure = e;
        }

        // Browser went away: fail outstanding commands and end the event stream
        var closed = new CdpClosedException(failure);
        _closed = closed;
        foreach (var pending in _pending.Values)
        {
            pending.TrySetException(closed);
        }

        _events.Writer.TryComplete();
    }

    private void Dispatch(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject message)
        {
            return;
        }

        if (message["id"]?.GetValue<int>() is { } id && _pending.TryRemove(id, out var reply))
        {
            if (message["error"] is JsonObject error)
            {
                reply.TrySetException(new CdpException(error["message"]?.GetValue<string>() ?? error.ToJsonString()));
            }
            else
            {
                reply.TrySetResult(message["result"] as JsonObject ?? new JsonObject());
            }

            return;
        }

        if (message["method"]?.GetValue<string>() is { } method)
        {
            _events.Writer.TryWrite(new CdpEvent(
                method, message["params"] as JsonObject ?? new JsonObject(), message["sessionId"]?.GetValue<string>()));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _receiveCts.CancelAsync();
        if (_receiveLoop is not null)
        {
            await _receiveLoop.ConfigureAwait(false);
        }

        _socket.Dispose();
        _receiveCts.Dispose();
        _sendLock.Dispose();
    }
}

internal sealed class CdpException(string message) : Exception(message);

/// <summary>The DevTools connection closed — normally because the browser was closed by the user.</summary>
internal sealed class CdpClosedException(Exception? inner) : Exception("DevTools connection closed", inner);
