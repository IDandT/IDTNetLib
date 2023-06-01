using System.Net.WebSockets;

namespace IDTNetLib;


public class IDTWebSocket
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _socket;
    private readonly WebSocketMessageType _messageType;
    private DateTime _lastActivityTime;

    public Uri Uri { get => _uri; }
    public DateTime LastActivityTime { get => _lastActivityTime; }
    public WebSocketState State { get => _socket.State; }
    public bool Connected { get => _socket.State == WebSocketState.Open; }


    public IDTWebSocket(string uri, IDTWebSocketMode type)
    {
        _uri = new Uri(uri);
        _socket = new ClientWebSocket();
        _messageType = type == IDTWebSocketMode.Binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text;
        _lastActivityTime = DateTime.Now;
    }


    public async Task ConnectAsync()
    {
        if (_socket.State == WebSocketState.Open) throw new InvalidOperationException("Socket already opened");

        try
        {
            await _socket.ConnectAsync(_uri, CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task CloseAsync()
    {
        if (_socket.State == WebSocketState.Aborted || _socket.State == WebSocketState.Closed) return;

        try
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            // await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task SendAsync(byte[] buffer)
    {
        if (_socket.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            await _socket.SendAsync(buffer, _messageType, true, CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task<WebSocketReceiveResult> ReceiveAsync(byte[] outBuffer)
    {
        if (_socket.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            WebSocketReceiveResult result = await _socket.ReceiveAsync(outBuffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (_socket.State != WebSocketState.Closed)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    throw new WebSocketException($"Socket closed with status code {result.CloseStatus} and description {result.CloseStatusDescription}");
                }
            }

            _lastActivityTime = DateTime.Now;

            return result;
        }
        catch
        {
            throw;
        }
    }
}
