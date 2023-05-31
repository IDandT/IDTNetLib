using System.Collections.Concurrent;
using System.Net;

namespace IDTNetLib;


/// <summary>
/// TCP Client class.
/// Creates a TCP client and handles all the data reception and transmission.
/// </summary>
public class IDTTcpClient
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
    private readonly IDTSocket? _clientSocket;
    private byte[] _readBuffer;
    private bool _running;
    private readonly IDTStatistics _statistics;

    // Message queue.
    private readonly ConcurrentQueue<IDTMessage> _messageQueue = new ConcurrentQueue<IDTMessage>();

    // Public properties.
    public int PendingMessages { get => _messageQueue.Count; }
    public IDTStatistics Statistics { get => _statistics; }
    public bool Connected { get => _clientSocket?.Connected ?? false; }
    public IPEndPoint EndPoint { get => _endPoint; }
    public bool IsRunning { get => _running; }

    // Background task for accepting new connections.
    private CancellationTokenSource _cancellationTokenSource;

    // Public events.
    public EventHandler<IDTMessage>? OnProcessMsg;
    public EventHandler<IDTSocket>? OnConnect;
    public EventHandler<IDTSocket>? OnDisconnect;


    // Client constructor.
    public IDTTcpClient(string ip, int port)
    {
        try
        {
            _ipAddr = IPAddress.Parse(ip);
            _endPoint = new IPEndPoint(_ipAddr, port);
            _clientSocket = new IDTSocket(ip, port, IDTProtocol.TCP);
            _readBuffer = new byte[BUFFER_SIZE];
            _running = false;
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

        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _clientSocket.Connect();

            _statistics.Reset();

            _running = true;

            // Start handling data reception.
            var receiveTask = Task.Run(() => StartReceiveAsync(_clientSocket, _cancellationTokenSource.Token));

            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages(_cancellationTokenSource.Token));

            // Launch event.
            OnConnect?.Invoke(this, _clientSocket);
        }
        catch (AggregateException e)
        {
            throw e.InnerExceptions[0];
        }
    }


    // Disconnect from server.
    public void Disconnect()
    {
        if (!_running) throw new InvalidOperationException("Client is already stopped");

        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            _running = false;

            if (_clientSocket.Connected) OnDisconnect?.Invoke(this, _clientSocket);

            _clientSocket?.Close();

            // Stop background tasks.
            _cancellationTokenSource.Cancel();
        }
        catch
        {
            throw;
        }
    }


    // Start receiving data from server.
    private async Task StartReceiveAsync(IDTSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            _running = true;

            int remainingBufferBytes = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesReceived = 0;

                // Read data. If we have some remaining bytes from previous packet, read starting offset
                if (remainingBufferBytes == 0)
                {
                    bytesReceived = await socket.ReceiveAsync(_readBuffer);
                }
                else
                {
                    bytesReceived = await socket.ReceiveAsync(_readBuffer, remainingBufferBytes, BUFFER_SIZE - remainingBufferBytes);
                }

                if (cancellationToken.IsCancellationRequested) break;

                _statistics.ReceiveOperations++;
                _statistics.BytesReceived += bytesReceived;

                if (bytesReceived == 0)
                {
                    throw new Exception("Connection closed by client");
                }

                // Generate packets from buffer data.
                int packetsReceived = IDTPacketizer.PacketizeBuffer(_messageQueue, socket, bytesReceived, ref _readBuffer, ref remainingBufferBytes);

                _statistics.PacketsReceived += packetsReceived;
            }

            _running = false;
        }
        catch
        {
            _running = false;

            _statistics.ConnectionsDropped++;

            if (socket.Connected) OnDisconnect?.Invoke(this, socket);

            socket?.Close();

            throw;
        }
    }


    // Sends one packet over the socket.
    public int Send(IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Client is stopped");
        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = _clientSocket.Send(packet.GetBytes());

            _statistics.SendOperations++;
            _statistics.PacketsSent++;
            _statistics.BytesSent += sentBytes;

            return sentBytes;
        }
        catch
        {
            throw;
        }
    }


    // Sends one packet over the socket asynchronously.
    public async Task<int> SendAsync(IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Client is stopped");
        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await _clientSocket.SendAsync(packet.GetBytes());

            _statistics.SendOperations++;
            _statistics.PacketsSent++;
            _statistics.BytesSent += sentBytes;

            return sentBytes;
        }
        catch
        {
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
