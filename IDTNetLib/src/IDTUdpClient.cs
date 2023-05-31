using System.Collections.Concurrent;
using System.Net;

namespace IDTNetLib;


/// <summary>
/// UDP Client class.
/// Creates a UDP client and handles all the data reception and transmission.
/// </summary>
public class IDTUdpClient
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
    public IDTUdpClient(string ip, int port)
    {
        try
        {
            _ipAddr = IPAddress.Parse(ip);
            _endPoint = new IPEndPoint(_ipAddr, port);
            _clientSocket = new IDTSocket(ip, port, IDTProtocol.UDP);
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


    // Open connection to server. Not really a connection because UDP is connectionless but starts cient tasks.
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


    // Disconnect from server. Not really a disconnect because UDP is connectionless but stop client.
    public void Disconnect()
    {
        if (!_running) throw new InvalidOperationException("Client is already stopped");

        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            _running = false;

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
            int remainingBufferBytes = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                UDPReceiveResult result;

                // Read data. If we have some remaining bytes from previous packet, read starting offset
                if (remainingBufferBytes == 0)
                {
                    result = await socket.ReceiveFromAsync(_readBuffer);
                }
                else
                {
                    result = await socket.ReceiveFromAsync(_readBuffer, remainingBufferBytes, BUFFER_SIZE - remainingBufferBytes);
                }

                if (cancellationToken.IsCancellationRequested) break;

                _statistics.ReceiveOperations++;
                _statistics.BytesReceived += result.BytesReceived;


                if (result.BytesReceived == 0)
                {
                    throw new Exception("Connection closed by client");
                }


                // Generate packets from buffer data.
                int packetsReceived = IDTPacketizer.PacketizeBuffer(_messageQueue, socket, result.BytesReceived, ref _readBuffer,
                    ref remainingBufferBytes, result.EndPoint);

                _statistics.PacketsReceived += packetsReceived;
            }
        }
        catch
        {
            _statistics.ConnectionsDropped++;

            OnDisconnect?.Invoke(this, socket);

            throw;
        }
    }


    // Sends one packet over the socket to specified remote endpoint.
    public int SendTo(IDTPacket packet, EndPoint? remoteEndPoint = null)
    {
        if (!_running) throw new InvalidOperationException("Client is stopped");
        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = _clientSocket.SendTo(packet.GetBytes(), remoteEndPoint ?? _endPoint);

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


    // Sends one packet over the socket asynchronously to specified remote endpoint.
    public async Task<int> SendToAsync(IDTPacket packet, EndPoint? remoteEndPoint = null)
    {
        if (!_running) throw new InvalidOperationException("Client is stopped");
        if (_clientSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await _clientSocket.SendToAsync(packet.GetBytes(), remoteEndPoint ?? _endPoint);

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
