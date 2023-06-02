using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace IDTNetLib;


public class IDTWebSocketServer
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private bool _running;
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
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
    public bool IsRunning { get => _running; }

    // Background task for accepting new connections.
    private CancellationTokenSource _cancellationTokenSource;

    // Public events.
    public EventHandler<IDTSocket>? OnConnect;
    public EventHandler<IDTSocket>? OnDisconnect;
    public EventHandler<IDTMessage>? OnProcessMsg;


    // Server constructor.
    public IDTWebSocketServer(string ip, int port)
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


    // Starts run server. Create a socket and launch accept background task.
    public void Start()
    {
        if (_running) throw new InvalidOperationException("Server is already running");

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            _listenerSocket = new IDTSocket(_endPoint.Address.ToString(), _endPoint.Port, IDTProtocol.TCP);

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
                IDTSocket newClientSocket = await _listenerSocket.AcceptAsync(IDTProtocol.WEBSOCKET);

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
                    // Console.WriteLine("with remaining");
                }

                if (cancellationToken.IsCancellationRequested) break;

                _statistics.ReceiveOperations++;
                _statistics.BytesReceived += bytesReceived;

                byte[] frame = new byte[bytesReceived];
                Buffer.BlockCopy(clientReadBuffer, 0, frame, 0, bytesReceived);

                FrameDecode(socket, frame);
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
            }

            _statistics.ConnectionsDropped++;
        }
    }


    // Based on source code from: 
    //
    // https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
    // 
    // Opcode 	Meaning 	 
    // ------  ----------------------
    // 0	    Continuation Frame
    // 1	    Text Fram
    // 2	    Binary Frame
    // 3-7	    Unassigned	
    // 8	    Connection Close Frame
    // 9	    Ping Frame
    // 10       Pong Frame
    // 11-15    Unassigned	
    private void FrameDecode(IDTSocket socket, byte[] bytes)
    {
        string s = Encoding.UTF8.GetString(bytes);

        if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
        {
            // Console.WriteLine("=====Handshaking from client=====\n{0}", s);

            // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
            // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
            // 3. Compute SHA-1 and Base64 hash of the new value
            // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
            string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
            string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] response = Encoding.UTF8.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

            // Handshake reply.
            socket.Send(response);

            // Flag that the handshake is done.
            socket.HandShakeDone = true;
        }
        else
        {
            bool fin = (bytes[0] & 0b10000000) != 0;
            bool mask = (bytes[1] & 0b10000000) != 0;   // must be true, "All messages from the client to the server have this bit set"
            int opcode = bytes[0] & 0b00001111;         // expecting 1 - text message
            int offset = 2;
            ulong msglen = (ulong)(bytes[1] & 0b01111111);

            // Handle close frame or pin/pong frames.
            if (opcode == 8)
            {
                // Close socket.
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

                return;
            }
            else if (opcode == 10)
            {
                return;
            }


            if (msglen == 126)
            {
                // Bytes are reversed because websocket will print them in Big-Endian, whereas
                // BitConverter will want them arranged in little-endian on windows
                msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                offset = 4;
            }
            else if (msglen == 127)
            {
                // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                // websocket frame available through client.Available).
                msglen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                offset = 10;
            }

            if (msglen == 0)
            {
                // No payload
            }
            else if (mask)
            {
                byte[] decoded = new byte[msglen];
                byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };

                offset += 4;

                for (ulong i = 0; i < msglen; ++i)
                {
                    decoded[i] = (byte)(bytes[offset + (int)i] ^ masks[i % 4]);
                }

                // Enqueue message.
                IDTPacket packet = new IDTPacket(decoded);
                IDTMessage message = new IDTMessage(socket.RemoteEndPoint, socket, packet);

                _messageQueue.Enqueue(message);

                _statistics.PacketsReceived++;
            }
            // else
            // {
            //     Console.WriteLine("mask bit not set");
            // }
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
    public int Send(IDTSocket socket, IDTPacket packet)
    {
        if (!_running) throw new InvalidOperationException("Server is stopped");
        if (socket is null || !socket.Connected) throw new NullReferenceException("Socket not connected");

        try
        {
            byte[] buffer = BuildResponseFrame(packet.Body!);

            int sentBytes = socket.Send(buffer);

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

            byte[] buffer = BuildResponseFrame(packet.Body!);

            foreach (IDTSocket socket in _connectionList)
            {
                if (socket.Connected)
                {
                    int sentBytes = socket.Send(buffer);

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

            byte[] buffer = BuildResponseFrame(packet.Body!);

            foreach (IDTSocket socket in clients)
            {
                if (socket.Connected)
                {
                    int sentBytes = socket.Send(buffer);

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


    // Generate frame for a response to request.
    public static byte[] BuildResponseFrame(byte[] data)
    {
        int dataLength = data.Length;
        byte[] frame;

        if (dataLength <= 125)
        {
            frame = new byte[2 + dataLength];
            frame[1] = (byte)dataLength;
        }
        else if (dataLength <= ushort.MaxValue)
        {
            frame = new byte[4 + dataLength];
            frame[1] = 126;
            frame[2] = (byte)(dataLength >> 8);
            frame[3] = (byte)(dataLength & 0xFF);
        }
        else
        {
            frame = new byte[10 + dataLength];
            frame[1] = 127;
            frame[2] = (byte)((dataLength >> 56) & 0xFF);
            frame[3] = (byte)((dataLength >> 48) & 0xFF);
            frame[4] = (byte)((dataLength >> 40) & 0xFF);
            frame[5] = (byte)((dataLength >> 32) & 0xFF);
            frame[6] = (byte)((dataLength >> 24) & 0xFF);
            frame[7] = (byte)((dataLength >> 16) & 0xFF);
            frame[8] = (byte)((dataLength >> 8) & 0xFF);
            frame[9] = (byte)(dataLength & 0xFF);
        }

        // TODO: Check if this is correct. Option for select mode? 0x81 is text i think.
        frame[0] = 0x82; // FIN bit set, opcode for binary data

        Buffer.BlockCopy(data, 0, frame, frame.Length - dataLength, dataLength);

        return frame;
    }
}