using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace IDTNetLib;


public class IDTWebSocketClient
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private readonly Uri _uri;
    private readonly IDTWebSocket? _socket;
    private readonly byte[] _readBuffer;
    private bool _running;
    private readonly IDTStatistics _statistics;

    // Message queue.
    private readonly ConcurrentQueue<IDTMessage> _messageQueue = new ConcurrentQueue<IDTMessage>();

    // Public properties.
    public int PendingMessages { get => _messageQueue.Count; }
    public IDTStatistics Statistics { get => _statistics; }
    public bool Connected { get => _socket?.State == WebSocketState.Open; }
    public Uri Uri { get => _uri; }

    // Public events.
    public EventHandler<IDTMessage>? OnProcessMsg;
    public EventHandler<IDTWebSocket>? OnConnect;
    public EventHandler<IDTWebSocket>? OnDisconnect;


    public IDTWebSocketClient(string uri)
    {
        try
        {
            _uri = new Uri(uri);
            _socket = new IDTWebSocket(uri);
            _running = false;
            _readBuffer = new byte[BUFFER_SIZE];
            _statistics = new IDTStatistics();
        }
        catch
        {
            throw;
        }
    }


    public void Connect()
    {
        if (_socket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            _socket.ConnectAsync().Wait();

            _statistics.Reset();

            _running = true;

            // Start handling data reception.
            var receiveTask = Task.Run(() => StartReceiveAsync(_socket));

            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages());

            // Launch event.
            OnConnect?.Invoke(this, _socket);
        }
        catch
        {
            throw;
        }
    }


    public void Disconnect()
    {
        if (_socket is null) throw new NullReferenceException("Socket not connected");
        if (_socket.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            _running = false;

            if (_socket.Connected) OnDisconnect?.Invoke(this, _socket);

            _socket?.CloseAsync().Wait();
        }
        catch
        {
            throw;
        }
    }


    public void Send(IDTPacket packet)
    {
        if (_socket is null) throw new NullReferenceException("Socket not created");
        if (_socket.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            // _socket.SendAsync(packet.GetBytes()).Wait();
            _socket.SendAsync(packet.Body!).Wait();


            _statistics.SendOperations++;
            _statistics.PacketsSent++;
            _statistics.BytesSent += packet.Lenght;
        }
        catch
        {
            throw;
        }
    }


    // Start receiving data from server.
    private async Task StartReceiveAsync(IDTWebSocket socket)
    {
        try
        {
            _running = true;


            while (_running)
            {
                try
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(_readBuffer);

                    _statistics.ReceiveOperations++;
                    _statistics.BytesReceived += result.Count;

                    // Console.WriteLine(Encoding.ASCII.GetString(_readBuffer, 0, result.Count));

                    IDTPacket packet = new IDTPacket();
                    packet.Lenght = result.Count;
                    packet.Body = new byte[result.Count];
                    Array.Copy(_readBuffer, packet.Body, result.Count);

                    IDTMessage message = new IDTMessage(socket.Uri, socket, packet);


                    _messageQueue.Enqueue(message);

                    _statistics.PacketsReceived++;
                }
                catch
                {
                    throw;
                }
            }

            _running = false;
        }
        catch
        {
            _running = false;

            _statistics.ConnectionsDropped++;

            if (socket.Connected) OnDisconnect?.Invoke(this, socket);

            socket?.CloseAsync();

            throw;
        }
    }


    // Message processing task. Launch one event for every message received.
    private async Task StartProcessingMessages()
    {
        try
        {
            while (_running || !_messageQueue.IsEmpty)
            {
                if (!_messageQueue.IsEmpty && OnProcessMsg != null)
                {
                    if (_messageQueue.TryDequeue(out IDTMessage? message))
                    {
                        OnProcessMsg?.Invoke(this, message);
                        _statistics.ProcessedMessages++;
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        catch
        {
            throw;
        }
    }
}