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
    private bool _quit;
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
            _quit = false;
            _running = false;

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
        try
        {
            _listenerSocket = new IDTSocket(_endPoint.Address.ToString(), _endPoint.Port, IDTProtocol.UDP);

            _listenerSocket.Bind(_endPoint.Port);

            _statistics.Reset();

            _running = true;

            // Start handling data reception.
            var receiveTask = Task.Run(() => StartReceiveAsync(_listenerSocket));

            // Start processing message Queue.
            var dequeueTask = Task.Run(() => StartProcessingMessages());

            // Start checking inactive clients.
            var checkTask = Task.Run(() => StartCheckingInactiveClients());
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


    // We have one running task of this method for every connected client.
    private async Task StartReceiveAsync(IDTSocket socket)
    {
        try
        {
            int clientRemainingBufferBytes = 0;
            byte[] clientReadBuffer = new byte[BUFFER_SIZE];

            while (_running && !_quit)
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
                        await Task.Delay(500);

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

            _running = false;
        }
        catch
        {
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
    public int SendTo(IDTPacket packet, EndPoint endPoint)
    {
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
        try
        {
            int totalSentBytes = 0;

            foreach (UDPClientInfo endPoint in _connectionList)
            {
                int sentBytes = _listenerSocket!.SendTo(packet.GetBytes(), endPoint.EndPoint);

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
        if (clients.Length == 0) return 0;

        try
        {
            int totalSentBytes = 0;

            foreach (UDPClientInfo endPoint in clients)
            {
                int sentBytes = _listenerSocket!.SendTo(packet.GetBytes(), endPoint.EndPoint);

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
    public async Task StartCheckingInactiveClients()
    {
        if (_listenerSocket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            while (_running && !_quit)
            {
                // Check clients every N seconds
                await Task.Delay(TimeSpan.FromSeconds(_clientPingTimeout));

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