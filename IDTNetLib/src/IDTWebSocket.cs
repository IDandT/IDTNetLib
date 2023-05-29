using System.Net.WebSockets;

namespace IDTNetLib;


public class IDTWebSocket
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _client;
    private DateTime _lastActivityTime;

    public Uri Uri { get => _uri; }
    public DateTime LastActivityTime { get => _lastActivityTime; }
    public WebSocketState State { get => _client.State; }
    public bool Connected { get => _client.State == WebSocketState.Open; }


    public IDTWebSocket(string uri)
    {
        _uri = new Uri(uri);
        _client = new ClientWebSocket();
        _lastActivityTime = DateTime.Now;
    }


    public async Task ConnectAsync()
    {
        if (_client.State == WebSocketState.Open) throw new InvalidOperationException("Socket already opened");

        try
        {
            await _client.ConnectAsync(_uri, CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task CloseAsync()
    {
        if (_client.State != WebSocketState.Open) throw new InvalidOperationException("Socket not connected");

        try
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task SendAsync(byte[] buffer)
    {
        try
        {
            await _client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            throw;
        }
    }


    public async Task<WebSocketReceiveResult> ReceiveAsync(byte[] outBuffer)
    {
        if (_client.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            WebSocketReceiveResult result = await _client.ReceiveAsync(outBuffer, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (_client.State != WebSocketState.Closed)
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
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
