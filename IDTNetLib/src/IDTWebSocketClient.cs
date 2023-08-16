using System.Collections.Concurrent;
using System.Net.WebSockets;

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
    public bool IsRunning { get => _running; }

    // Background task for accepting new connections.
    private CancellationTokenSource _cancellationTokenSource;

    // Public events.
    public EventHandler<IDTMessage>? OnProcessMsg;
    public EventHandler<IDTWebSocket>? OnConnect;
    public EventHandler<IDTWebSocket>? OnDisconnect;


    // Client constructor.
    public IDTWebSocketClient(string uri)
    {
        try
        {
            _uri = new Uri(uri);
            _socket = new IDTWebSocket(uri, IDTWebSocketMode.Binary);
            _running = false;
            _readBuffer = new byte[BUFFER_SIZE];
            _cancellationTokenSource = new CancellationTokenSource();

            _statistics = new IDTStatistics();
        }
        catch
        {
            throw;
        }
    }


    // Open connection to server.
    public void Connect()
    {
        if (_running) throw new InvalidOperationException("Client is already running");
        if (_socket is null) throw new NullReferenceException("Socket not connected");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _socket.ConnectAsync().Wait();

            _statistics.Reset();

            _running = true;

            // Start handling data reception.
            var receiveTask = Task.Run(() => StartReceiveAsync(_socket, _cancellationTokenSource.Token));

            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages(_cancellationTokenSource.Token));

            // Launch event.
            OnConnect?.Invoke(this, _socket);
        }
        catch
        {
            throw;
        }
    }


    // Disconnect from server.
    public void Disconnect()
    {
        if (_socket is null) throw new NullReferenceException("Socket not connected");
        if (_socket.State != WebSocketState.Open) throw new WebSocketException("Socket not connected");

        try
        {
            _running = false;

            if (_socket.Connected) OnDisconnect?.Invoke(this, _socket);

            _socket?.CloseAsync().Wait();

            // Stop background tasks.
            _cancellationTokenSource.Cancel();
        }
        catch
        {
            throw;
        }
    }


    // Sends one packet over the socket.
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
            _statistics.BytesSent += packet.Length;
        }
        catch
        {
            throw;
        }
    }


    // Start receiving data from server.
    private async Task StartReceiveAsync(IDTWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(_readBuffer);

                    _statistics.ReceiveOperations++;
                    _statistics.BytesReceived += result.Count;

                    // Console.WriteLine(Encoding.ASCII.GetString(_readBuffer, 0, result.Count));

                    IDTPacket packet = new IDTPacket();
                    packet.Length = result.Count;
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

            OnDisconnect?.Invoke(this, socket);

            socket?.CloseAsync();

            throw;
        }
    }


    // Message processing task. Launch one event for every message received.
    private async Task StartProcessingMessages(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
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
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        catch
        {
            throw;
        }
    }
}