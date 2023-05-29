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
    private bool _quit;
    private bool _running;
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
    private readonly IDTProtocol _protocol;
    private IDTSocket? _listenerSocket;
    private int _maximumClients = 2;
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
            _quit = false;
            _running = false;

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
        try
        {
            _listenerSocket = new IDTSocket(_endPoint.Address.ToString(), _endPoint.Port, _protocol);

            _listenerSocket.Bind(_endPoint.Port);

            _statistics.Reset();

            _listenerSocket.Listen(10);

            // Start accepting new connections.
            Task.Run(() => StartAccept());


            // New thread for long running task?
            // Thread acceptThread = new Thread(async () => await StartAccept());
            // acceptThread.Start();
            // acceptThread.Join();
        }
        catch (AggregateException e)
        {
            throw e.InnerExceptions[0];
        }
    }


    // Stop server. Close listener socket.
    public void Stop()
    {
        try
        {
            _running = false;
            _listenerSocket?.Close();
            _listenerSocket = null;
        }
        catch
        {
            throw;
        }
    }


    // Quit main loop from server.
    public void Quit()
    {
        _quit = true;
    }


    // Task than listen for new clients. Creates a background receive task for every accepted client.
    private async Task StartAccept()
    {
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        _running = true;

        try
        {
            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages());

            while (!_quit)
            {
                IDTSocket newClientSocket = await _listenerSocket.AcceptAsync();

                _running = true;

                // Check connection limit.
                if (_connectionList.Count >= _maximumClients)
                {
                    // Send error message to client.                    
                    int sentBytes = newClientSocket.Send(IDTPacket.CreateFromString("Server is full").GetBytes());


                    await Task.Delay(500);

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
                var receiveTask = Task.Run(() => StartReceiveAsync(newClientSocket));


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
    private async Task StartReceiveAsync(IDTSocket socket)
    {
        try
        {
            int clientRemainingBufferBytes = 0;
            byte[] clientReadBuffer = new byte[BUFFER_SIZE];

            while (_running)
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

            _running = false;
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
                if (_connectionList.Count == 0)
                {
                    _running = false;
                }
            }

            _statistics.ConnectionsDropped++;
        }
    }


    // Message processing task. Launch one event for every message received.
    private async Task StartProcessingMessages()
    {
        try
        {
            while (_running || !_messageQueue.IsEmpty)
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
                    await Task.Delay(100);
                }
            }
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