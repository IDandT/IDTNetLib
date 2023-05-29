using System.Net;

namespace IDTNetLib;


/// <summary>
/// A final message structure that contains packet and complementary information.
/// </summary>
public class IDTMessage
{
    // Private fields.
    private readonly EndPoint? _remoteEndPoint;
    private readonly Uri? _remoteUri;
    private readonly IDTSocket? _sourceSocket;
    private readonly IDTWebSocket? _sourceWebSocket;
    private readonly IDTPacket _packet;

    // Public properties.
    public bool IsWebSocketMessage { get => _sourceWebSocket != null; }
    public EndPoint? RemoteEndPoint { get => _remoteEndPoint; }
    public Uri? RemoteUri { get => _remoteUri; }
    public IDTSocket? SourceSocket { get => _sourceSocket; }
    public IDTWebSocket? SourceWebSocket { get => _sourceWebSocket; }
    public IDTPacket Packet { get => _packet; }


    // Constructor for message from packet.
    public IDTMessage(EndPoint? remoteEndPoint, IDTSocket? sourceSocket, IDTPacket packet)
    {
        _remoteEndPoint = remoteEndPoint;
        _sourceSocket = sourceSocket;
        _sourceWebSocket = null;
        _packet = packet;
    }

    // Constructor for message from packet.
    public IDTMessage(Uri? remoteUri, IDTWebSocket? sourceWebSocket, IDTPacket packet)
    {
        _remoteEndPoint = null;
        _remoteUri = remoteUri;
        _sourceSocket = null;
        _sourceWebSocket = sourceWebSocket;
        _sourceWebSocket = null;
        _packet = packet;
    }
}
