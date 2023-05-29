using System.Net;

namespace IDTNetLib;


// Constants like buffer size etc.
public class IDTConstants
{
    public const int BUFFER_SIZE = 2048;
}


// Supported protocols
public enum IDTProtocol
{
    TCP,
    UDP,
}


// Struct for async data reception. Task returns this structs, and filled out buffer as argument.
public class UDPReceiveResult
{
    public EndPoint EndPoint { get; set; }
    public int BytesReceived { get; set; }

    public UDPReceiveResult(EndPoint endPoint, int bytesReceived)
    {
        EndPoint = endPoint;
        BytesReceived = bytesReceived;
    }
}


// Struct for store UDP client end point and last received packet time (for client timeouts).
public class UDPClientInfo
{
    public EndPoint EndPoint { get; set; }
    public DateTime LastActivityTime { get; set; }

    public UDPClientInfo(EndPoint endPoint)
    {
        EndPoint = endPoint;
        LastActivityTime = DateTime.Now;
    }
}