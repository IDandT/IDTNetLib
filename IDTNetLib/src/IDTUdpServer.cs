using System.Collections.Concurrent;
using System.Net;

namespace IDTNetLib;


/// <summary>
/// UDP Server class.
/// Creates a UDP server and handles all the data reception and transmission.
/// </summary>
public class IDTUdpServer
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private bool _running;
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
    private IDTSocket? _listenerSocket;
    private int _maximumClients = 4;
    private int _clientPingTimeout = 60;
    private int _clientRemoveTimeout = 120;
    private readonly List<UDPClientInfo> _connectionList = new List<UDPClientInfo>();
    private readonly IDTStatistics _statistics;

    // Message queue.
    private readonly ConcurrentQueue<IDTMessage> _messageQueue = new ConcurrentQueue<IDTMessage>();

    // Public properties.
    public List<UDPClientInfo> ConnectionList { get => _connectionList; }
    public int MaximumClients { get => _maximumClients; set => _maximumClients = value; }
    public int ClientCheckTiemout { get => _clientPingTimeout; set => _clientPingTimeout = value; }
    public int ClientRemoveTimeout { get => _clientRemoveTimeout; set => _clientRemoveTimeout = value; }
    public int PendingMessages { get => _messageQueue.Count; }
    public IDTStatistics Statistics { get => _statistics; }
    public IPEndPoint EndPoint { get => _endPoint; }
    public bool IsRunning { get => _running; }

    // Background task for accepting new connections.
    private CancellationTokenSource _cancellationTokenSource;

    // Public events.
    public EventHandler<UDPClientInfo>? OnConnect;
    public EventHandler<UDPClientInfo>? OnDisconnect;
    public EventHandler<IDTMessage>? OnProcessMsg;


    // Server constructor.
    public IDTUdpServer(string ip, int port)
    {
        try
        {
            _ipAddr = IPAddress.Parse(ip);
            _endPoint = new IPEndPoint(_ipAddr, port);
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


    // Starts run server. Create a socket and launch some background tasks.
    public void Start()
    {
        if (_running) throw new InvalidOperationException("Server is already running");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _listenerSocket = new IDTSocket(_endPoint.Address.ToString(), _endPoint.Port, IDTProtocol.UDP);

            _listenerSocket.Bind(_endPoint.Port);

            _statistics.Reset();

            // Start handling data reception.
            var receiveTask = Task.Run(() => StartReceiveAsync(_listenerSocket, _cancellationTokenSource.Token));

            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages(_cancellationTokenSource.Token));

            // Start checking inactive clients.
            var checkTask = Task.Run(() => StartCheckingInactiveClients(_cancellationTokenSource.Token));

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
            foreach (UDPClientInfo client in _connectionList.ToList())
            {
                _connectionList.Remove(client);
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


    // We have one running task of this method for every connected client.
    private async Task StartReceiveAsync(IDTSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            int clientRemainingBufferBytes = 0;
            byte[] clientReadBuffer = new byte[BUFFER_SIZE];

            while (!cancellationToken.IsCancellationRequested)
            {
                UDPReceiveResult result;

                // Read data from client. If we have some remaining bytes from previous packet, read starting offset.
                try
                {
                    if (clientRemainingBufferBytes == 0)
                    {
                        result = await socket.ReceiveFromAsync(clientReadBuffer);
                    }
                    else
                    {
                        result = await socket.ReceiveFromAsync(clientReadBuffer, clientRemainingBufferBytes, BUFFER_SIZE - clientRemainingBufferBytes);
                    }
                }
                catch
                {
                    // On client reception error, keep listening. Background process
                    // Will remove client from list.
                    continue;
                }

                if (cancellationToken.IsCancellationRequested) break;

                // Check if is new client, or it is in endpoints client list
                int index = _connectionList.FindIndex(x => x.EndPoint.ToString() == result.EndPoint.ToString());

                if (index == -1)
                {
                    // Check connection limit. If reached, reject client
                    if (_connectionList.Count >= _maximumClients)
                    {
                        // Send error message to client                    
                        int sentBytes = _listenerSocket!.SendTo(IDTPacket.CreateFromString("Server is full").GetBytes(), result.EndPoint);

                        // keep listening
                        await Task.Delay(500, cancellationToken);

                        _statistics.SendOperations++;
                        _statistics.BytesSent += sentBytes;
                        _statistics.PacketsSent++;
                        _statistics.ConnectionsRejected++;

                        continue;
                    }
                    else
                    {
                        // Client accepted, add info to udp endpoint list
                        UDPClientInfo newClient = new UDPClientInfo(result.EndPoint);

                        _connectionList.Add(newClient);


                        _statistics.ConnectionsAccepted++;
                        _statistics.ConnectionsPeak = _connectionList.Count > _statistics.ConnectionsPeak
                                                    ? _connectionList.Count
                                                    : _statistics.ConnectionsPeak;

                        OnConnect?.Invoke(this, newClient);
                    }
                }
                else
                {
                    // Update client activity
                    _connectionList[index].LastActivityTime = DateTime.Now;
                }


                _statistics.ReceiveOperations++;
                _statistics.BytesReceived += result.BytesReceived;


                // Console.WriteLine("BEFORE:");
                // IDTUtils.PrintBuffer(clientReadBuffer);

                // Generate packets from buffer data.
                int packetsReceived = IDTPacketizer.PacketizeBuffer(_messageQueue, socket, result.BytesReceived,
                    ref clientReadBuffer, ref clientRemainingBufferBytes, result.EndPoint);

                // Console.WriteLine("AFTER:");
                // IDTUtils.PrintBuffer(clientReadBuffer);

                _statistics.PacketsReceived += packetsReceived;
            }
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
    public int SendTo(IDTPacket packet, EndPoint endPoint)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = _listenerSocket.SendTo(packet.GetBytes(), endPoint);

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


    // Sends one packet over the socket asynchronously to remote endpoint.
    public async Task<int> SendToAsync(IDTPacket packet, EndPoint endPoint)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await _listenerSocket.SendToAsync(packet.GetBytes(), endPoint);

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

            byte[] buffer = packet.GetBytes();

            foreach (UDPClientInfo endPoint in _connectionList)
            {
                int sentBytes = _listenerSocket!.SendTo(buffer, endPoint.EndPoint);

                totalSentBytes += sentBytes;

                _statistics.SendOperations++;
                _statistics.PacketsSent++;
                _statistics.BytesSent += sentBytes;
            }

            return totalSentBytes;
        }
        catch
        {
            throw;
        }
    }


    // Send a packet to specified clients.
    public int BroadcastTo(UDPClientInfo[] clients, IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");

        if (clients.Length == 0) return 0;

        try
        {
            int totalSentBytes = 0;

            byte[] buffer = packet.GetBytes();

            foreach (UDPClientInfo endPoint in clients)
            {
                int sentBytes = _listenerSocket!.SendTo(buffer, endPoint.EndPoint);

                totalSentBytes += sentBytes;

                _statistics.SendOperations++;
                _statistics.PacketsSent++;
                _statistics.BytesSent += sentBytes;
            }

            return totalSentBytes;
        }
        catch
        {
            throw;
        }
    }


    // UDP is connectionless, so we need to periodically check for inactive clients. We have 2 timeout intervals:
    // Ping timeout:   is the time between last activity time, and ping request to client.
    // Remove timeout: is the time between last activity time, and client "disconnection".
    public async Task StartCheckingInactiveClients(CancellationToken cancellationToken)
    {
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check clients every N seconds
                await Task.Delay(TimeSpan.FromSeconds(_clientPingTimeout), cancellationToken);

                if (_connectionList.Count > 0)
                {

                    DateTime now = DateTime.Now;

                    // Remove clients that are inactive for more than X seconds (_clientRemoveTimeout).
                    foreach (UDPClientInfo client in _connectionList.ToList())
                    {
                        if (now.Subtract(client.LastActivityTime).TotalSeconds > _clientRemoveTimeout)
                        {

                            _statistics.ConnectionsDropped++;

                            _connectionList.Remove(client);

                            OnDisconnect?.Invoke(this, client);
                        }
                    }

                    // Send PING to remaining inactive clients (_clientPingTimeout).
                    foreach (UDPClientInfo client in _connectionList)
                    {
                        if (now.Subtract(client.LastActivityTime).TotalSeconds > _clientPingTimeout)
                        {
                            // FIXME: Special packet? special message check??
                            _listenerSocket.SendTo(IDTPacket.CreateFromString("$$PING$$").GetBytes(), client.EndPoint);
                        }
                    }
                }
            }
        }
        catch
        {
            throw;
        }
    }


}