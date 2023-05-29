using System.Net;
using System.Net.Sockets;

namespace IDTNetLib;

// Some options we can use to adjust socket behavior:
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, 0);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 50000);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 50000);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, BUFFER_SIZE);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, BUFFER_SIZE);
// _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);


/// <summary>
/// A new socket class for IDTNetLib that supports asyncronous operations.
/// </summary>
public class IDTSocket
{
    private const int BUFFER_SIZE = IDTConstants.BUFFER_SIZE;

    // Private fields.
    private readonly IPAddress _ipAddr;
    private readonly IPEndPoint _endPoint;
    private readonly IDTProtocol _protocol;
    private Socket? _socket;
    private byte[] _buffer;
    private DateTime _lastActivityTime;
    private bool _connected;

    // Public properties.
    public bool Connected { get => _connected; }
    public EndPoint? RemoteEndPoint { get => _socket?.RemoteEndPoint!; }
    public byte[] BufferData { get => _buffer; set => _buffer = value; }
    public DateTime LastActivityTime { get => _lastActivityTime; }


    // Constructor for new socket.
    public IDTSocket(string ip, int port, IDTProtocol protocol)
    {
        try
        {
            _ipAddr = IPAddress.Parse(ip);
            _endPoint = new IPEndPoint(_ipAddr, port);
            _protocol = protocol;
            _socket = null;
            _buffer = new byte[BUFFER_SIZE];
            _lastActivityTime = DateTime.Now;

            if (_protocol == IDTProtocol.TCP)
            {
                _socket = new Socket(_ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
            }
            else
            {
                _socket = new Socket(_ipAddr.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Constructor for create IDTAyncSocket from standar .NET socket.
    public IDTSocket(Socket socket)
    {
        try
        {
            _socket = socket;
            _ipAddr = ((IPEndPoint)_socket.RemoteEndPoint!).Address;
            _endPoint = new IPEndPoint(_ipAddr, ((IPEndPoint)_socket.RemoteEndPoint).Port);
            _protocol = socket.ProtocolType == ProtocolType.Tcp ? IDTProtocol.TCP : IDTProtocol.UDP;
            _buffer = new byte[BUFFER_SIZE];
            _lastActivityTime = DateTime.Now;
            _connected = true;

            if (_protocol == IDTProtocol.TCP)
            {
                _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Start connection to remote host.
    public void Connect()
    {
        try
        {
            _socket?.Connect(_endPoint);
            _connected = true;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Close connection from remote host.
    public void Close()
    {
        if (_protocol != IDTProtocol.TCP) throw new Exception("Operation not supported for this protocol");

        try
        {
            _socket?.Shutdown(SocketShutdown.Both);
            _socket?.Close();
            _socket = null;
            _connected = false;
        }
        catch (Exception)
        {
            // Close connection normally ends if the socket is already closed.
            _connected = false;
        }
    }


    // Associate socket to specified port.
    public void Bind(int port)
    {
        if (_socket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            _socket.Bind(new IPEndPoint(_ipAddr, port));

            _lastActivityTime = DateTime.Now;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Start listening for incoming connections.
    public void Listen(int backlog)
    {
        if (_socket is null) throw new NullReferenceException("Socket not connected");

        try
        {
            _socket.Listen(backlog);

            _lastActivityTime = DateTime.Now;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Accept incoming connection asyncronously. Returns socket for new connection.
    public async Task<IDTSocket> AcceptAsync()
    {
        if (_protocol != IDTProtocol.TCP) throw new Exception("Operation not supported for this protocol");

        try
        {
            if (_socket is null)
            {
                Connect();
            }

            // Post accepts on the listening socket.
            Socket newSocket = await _socket!.AcceptAsync();

            _lastActivityTime = DateTime.Now;

            return new IDTSocket(newSocket);
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Send buffer data through socket.
    public int Send(byte[] sendBuffer)
    {
        if (_protocol != IDTProtocol.TCP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = _socket.Send(sendBuffer, SocketFlags.None);

            _lastActivityTime = DateTime.Now;

            return sentBytes;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Send buffer data to specified endpoint (for UDP).
    public int SendTo(byte[] sendBuffer, EndPoint? remoteEndPoint = null)
    {
        if (_protocol != IDTProtocol.UDP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = _socket.SendTo(sendBuffer, SocketFlags.None, remoteEndPoint!);

            _lastActivityTime = DateTime.Now;

            return sentBytes;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Send buffer data through socket asyncronously.
    public async Task<int> SendAsync(byte[] sendBuffer)
    {
        if (_protocol != IDTProtocol.TCP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await _socket.SendAsync(sendBuffer, SocketFlags.None);

            _lastActivityTime = DateTime.Now;

            return sentBytes;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Send buffer data to specified endpoint asyncronously (for UDP).
    public async Task<int> SendToAsync(byte[] sendBuffer, EndPoint? remoteEndPoint = null)
    {
        if (_protocol != IDTProtocol.UDP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            int sentBytes = await _socket.SendToAsync(sendBuffer, SocketFlags.None, remoteEndPoint!);

            _lastActivityTime = DateTime.Now;

            return sentBytes;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Start reception of data asyncronously. Stores new data in outBuffer and return number of bytes received.
    public async Task<int> ReceiveAsync(byte[] outBuffer)
    {
        if (_protocol != IDTProtocol.TCP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            int bytesReceived = await _socket.ReceiveAsync(_buffer, SocketFlags.None);

            _lastActivityTime = DateTime.Now;

            Buffer.BlockCopy(_buffer, 0, outBuffer, 0, bytesReceived);

            return bytesReceived;
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Start reception of data asyncronously from specified endpoint (UDP). 
    // Stores new data in outBuffer and return number of bytes received.
    public async Task<UDPReceiveResult> ReceiveFromAsync(byte[] outBuffer)
    {
        if (_protocol != IDTProtocol.UDP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);


            Task<int> receiveTask = Task.Factory.FromAsync(
                (callback, state) =>
                {
                    return _socket.BeginReceiveFrom(_buffer, 0, _buffer.Length, SocketFlags.None, ref remoteEP, callback, state);
                },
                ar =>
                {
                    return _socket.EndReceiveFrom(ar, ref remoteEP);
                },
                null);

            int bytesRead = await receiveTask;

            if (bytesRead > 0)
            {
                Array.Copy(_buffer, outBuffer, bytesRead);
            }

            // return bytesRead;
            return new UDPReceiveResult(remoteEP, bytesRead);
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Start reception of data asyncronously. Stores new data in outBuffer starting at offset and
    // reading "count" bytes.
    public async Task<int> ReceiveAsync(byte[] outBuffer, int offset, int count)
    {
        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            ArraySegment<byte> bufferSegmentToFill = new ArraySegment<byte>(_buffer, offset, count);

            int bytesReceived = await _socket.ReceiveAsync(bufferSegmentToFill, SocketFlags.None);

            _lastActivityTime = DateTime.Now;

            Buffer.BlockCopy(_buffer, offset, outBuffer, offset, bytesReceived);

            return bytesReceived;
        }
        catch (Exception)
        {
            throw;
        }
    }


    // Start reception of data asyncronously from specified endpoint (UDP). Stores new data in outBuffer 
    // starting at offset and reading "count" bytes.
    public async Task<UDPReceiveResult> ReceiveFromAsync(byte[] outBuffer, int offset, int count)
    {
        if (_protocol != IDTProtocol.UDP) throw new Exception("Operation not supported for this protocol");

        if (_socket is null || (_protocol == IDTProtocol.TCP && !_socket.Connected))
            throw new NullReferenceException("Socket not connected");

        try
        {
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            _socket.Bind(remoteEP);

            Task<int> receiveTask = Task.Factory.FromAsync(
                (callback, state) =>
                {
                    return _socket.BeginReceiveFrom(_buffer, offset, count, SocketFlags.None, ref remoteEP, callback, state);
                },
                ar =>
                {
                    return _socket.EndReceiveFrom(ar, ref remoteEP);
                },
                null);

            int bytesRead = await receiveTask;

            if (bytesRead > 0)
            {
                // Array.Copy(_buffer, outBuffer, bytesRead);
                Array.Copy(_buffer, 0, outBuffer, offset, bytesRead);
            }

            // return bytesRead;
            return new UDPReceiveResult(remoteEP, bytesRead);
        }
        catch (Exception)
        {
            throw;
        }
    }
}