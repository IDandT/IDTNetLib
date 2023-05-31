using System.Collections.Concurrent;
using System.Net;

namespace IDTNetLib;


/// <summary>
/// TCP Server class.
/// Creates a TCP server and handles all the data reception and transmission.
/// </summary>
public class IDTTcpServer
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private bool _running;
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
    private readonly IDTProtocol _protocol;
    private IDTSocket? _listenerSocket;
    private int _maximumClients = 4;
    private readonly List<IDTSocket> _connectionList = new List<IDTSocket>();
    private readonly IDTStatistics _statistics;

    // Message queue.
    private readonly ConcurrentQueue<IDTMessage> _messageQueue = new ConcurrentQueue<IDTMessage>();

    // Public properties.
    public List<IDTSocket> ConnectionList { get => _connectionList; }
    public int MaximumClients { get => _maximumClients; set => _maximumClients = value; }
    public int PendingMessages { get => _messageQueue.Count; }
    public IDTStatistics Statistics { get => _statistics; }
    public IPEndPoint EndPoint { get => _endPoint; }
    public bool IsRunning { get => _running; }

    // Background task for accepting new connections.
    private CancellationTokenSource _cancellationTokenSource;

    // Public events.
    public EventHandler<IDTSocket>? OnConnect;
    public EventHandler<IDTSocket>? OnDisconnect;
    public EventHandler<IDTMessage>? OnProcessMsg;


    // Server constructor.
    public IDTTcpServer(string ip, int port)
    {
        try
        {
            _ipAddr = IPAddress.Parse(ip);
            _endPoint = new IPEndPoint(_ipAddr, port);
            _protocol = IDTProtocol.TCP;
            _listenerSocket = null;
            _running = false;
            _cancellationTokenSource = new CancellationTokenSource();

            _statistics = new IDTStatistics();
        }
        catch
        {
            throw;
        }
    }


    // Starts run server. Create a socket and launch accept background task.
    public void Start()
    {
        if (_running) throw new InvalidOperationException("Server is already running");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _listenerSocket = new IDTSocket(_endPoint.Address.ToString(), _endPoint.Port, _protocol);

            _listenerSocket.Bind(_endPoint.Port);

            _statistics.Reset();

            _listenerSocket.Listen(10);

            // Start accepting new connections.
            Task acceptTask = Task.Run(() => StartAccept(_cancellationTokenSource.Token));

            // Start processing message Queue.
            Task dequeueTask = Task.Run(() => StartProcessingMessages(_cancellationTokenSource.Token));

            _running = true;
        }
        catch (AggregateException e)
        {
            throw e.InnerExceptions[0];
        }
    }


    // Stop server. Close listener socket.
    public void Stop()
    {
        if (!_running) throw new InvalidOperationException("Server is already stopped");

        try
        {
            // Disconnect all clients.
            foreach (IDTSocket s in _connectionList)
            {
                s.Close();
            }

            _running = false;
            _listenerSocket?.Close();
            _listenerSocket = null;

            // Stop background tasks.
            _cancellationTokenSource.Cancel();
        }

        catch
        {
            throw;
        }
    }


    // Task than listen for new clients. Creates a background receive task for every accepted client.
    private async Task StartAccept(CancellationToken cancellationToken)
    {
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IDTSocket newClientSocket = await _listenerSocket.AcceptAsync();

                if (cancellationToken.IsCancellationRequested) break;

                // Check connection limit.
                if (_connectionList.Count >= _maximumClients)
                {
                    // Send error message to client.                    
                    int sentBytes = newClientSocket.Send(IDTPacket.CreateFromString("Server is full").GetBytes());


                    await Task.Delay(500, cancellationToken);

                    // Close connection and keep listening.
                    newClientSocket.Close();

                    _statistics.SendOperations++;
                    _statistics.BytesSent += sentBytes;
                    _statistics.PacketsSent++;
                    _statistics.ConnectionsRejected++;

                    continue;
                }
                else
                {
                    // Add connection to list.
                    _connectionList.Add(newClientSocket);
                }

                // Start handling data reception for new client.
                var receiveTask = Task.Run(() => StartReceiveAsync(newClientSocket, cancellationToken), cancellationToken);

                _statistics.ConnectionsAccepted++;
                _statistics.ConnectionsPeak = _connectionList.Count > _statistics.ConnectionsPeak
                                            ? _connectionList.Count
                                            : _statistics.ConnectionsPeak;

                // Launch event.
                OnConnect?.Invoke(this, newClientSocket);
            }
        }
        catch (AggregateException e)
        {
            throw e.InnerExceptions[0];
        }
    }


    // We have one running task of this method for every connected client.
    private async Task StartReceiveAsync(IDTSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            int clientRemainingBufferBytes = 0;
            byte[] clientReadBuffer = new byte[BUFFER_SIZE];

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesReceived = 0;

                // Read data from client. If we have some remaining bytes from previous packet, read starting offset.
                if (clientRemainingBufferBytes == 0)
                {
                    bytesReceived = await socket.ReceiveAsync(clientReadBuffer);
                }
                else
                {
                    bytesReceived = await socket.ReceiveAsync(clientReadBuffer, clientRemainingBufferBytes, BUFFER_SIZE - clientRemainingBufferBytes);

                }

                if (cancellationToken.IsCancellationRequested) break;

                _statistics.ReceiveOperations++;
                _statistics.BytesReceived += bytesReceived;

                // If recieve 0 bytes, client closed connection.
                if (bytesReceived == 0)
                {
                    throw new Exception("Connection closed by client");
                }

                // Console.WriteLine("BEFORE:");
                // IDTUtils.PrintBuffer(clientReadBuffer);

                // Generate packets from buffer data.
                int packetsReceived = IDTPacketizer.PacketizeBuffer(_messageQueue, socket, bytesReceived,
                    ref clientReadBuffer, ref clientRemainingBufferBytes);

                // Console.WriteLine("AFTER:");
                // IDTUtils.PrintBuffer(clientReadBuffer);

                _statistics.PacketsReceived += packetsReceived;
            }

            Console.WriteLine("Receive task cancelled");
        }
        catch
        {
            // We launch first event to pass the full client info in event arg.
            // Socket will be close after the event handling.
            OnDisconnect?.Invoke(this, socket);

            socket.Close();
            if (_connectionList.Contains(socket))
            {
                _connectionList.Remove(socket);
            }

            _statistics.ConnectionsDropped++;
        }
    }


    // Message processing task. Launch one event for every message received.
    private async Task StartProcessingMessages(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_messageQueue.IsEmpty)
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

            Console.WriteLine("Message processing  task cancelled");
        }
        catch
        {
            throw;
        }
    }


    // Delete all messages from queue.
    public void ClearMessageQueue()
    {
        _messageQueue.Clear();
    }


    // Sends one packet over the socket.
    public int Send(IDTSocket socket, IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");
        if (socket is null || !socket.Connected) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = socket.Send(packet.GetBytes());

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
    public async Task<int> SendAsync(IDTSocket socket, IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");
        if (socket is null || !socket.Connected) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await socket.SendAsync(packet.GetBytes());

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


    // Send a packet to all connected clients.
    public int BroadcastToAll(IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");

        try
        {
            int totalSentBytes = 0;

            foreach (IDTSocket socket in _connectionList)
            {
                if (socket.Connected)
                {
                    int sentBytes = socket.Send(packet.GetBytes());

                    totalSentBytes += sentBytes;

                    _statistics.SendOperations++;
                    _statistics.PacketsSent++;
                    _statistics.BytesSent += sentBytes;
                }
            }

            return totalSentBytes;
        }
        catch
        {
            throw;
        }
    }


    // Send a packet to specified clients.
    public int BroadcastTo(IDTSocket[] clients, IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");

        if (clients.Length == 0) return 0;

        try
        {
            int totalSentBytes = 0;

            foreach (IDTSocket socket in clients)
            {
                if (socket.Connected)
                {
                    int sentBytes = socket.Send(packet.GetBytes());

                    totalSentBytes += sentBytes;

                    _statistics.SendOperations++;
                    _statistics.PacketsSent++;
                    _statistics.BytesSent += sentBytes;
                }
            }

            return totalSentBytes;
        }
        catch
        {
            throw;
        }
    }

}