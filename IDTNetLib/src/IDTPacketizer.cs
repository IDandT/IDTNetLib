using System.Collections.Concurrent;
using System.Net;

namespace IDTNetLib;


/// <summary>
/// Packetizer class.
/// It is used to create individual packets from a received buffer.
/// It has logic to split a buffer into multiple packets, or wait for new data if we dont
///  have enough data.
/// </summary>
public class IDTPacketizer
{
    public static int PacketizeBuffer(ConcurrentQueue<IDTMessage> _messageQueue, IDTSocket socket, int bytesRead,
        ref byte[] buffer, ref int remainingBufferBytes, EndPoint? udpEndPoint = null)
    {

        int packetCount = 0;

        try
        {
            if (bytesRead > 0)
            {
                int offset = 0;

                // Checks if we have enough bytes for read length.
                while (offset + 4 <= bytesRead)
                {
                    int packetLength = BitConverter.ToInt32(buffer, offset);

                    if (packetLength >= buffer.Length)
                    {
                        Console.WriteLine("Buffer too small");
                        throw new Exception("Buffer too small");
                    }

                    // Check if we have enoutgh bytes to read full packet body.
                    if (offset + 4 + packetLength > (bytesRead + remainingBufferBytes))
                    {
                        break;
                    }

                    // Creates new packet from the buffer segment..
                    byte[] packetData = new byte[packetLength];
                    Buffer.BlockCopy(buffer, offset + 4, packetData, 0, packetLength);


                    IDTPacket packet = new IDTPacket(packetData);

                    // Insert message in global queue. For UDP get udpEndPoint parameter. For TCP, endpoint from socket.
                    if (udpEndPoint is null)
                    {
                        _messageQueue.Enqueue(new IDTMessage(socket.RemoteEndPoint!, socket, packet));
                        packetCount++;
                    }
                    else
                    {
                        // For PING request, will send PONG response. PING/PONG not pass through queue.
                        if (packet.GetString() == "$$PING$$")
                        {
                            // Not enqueue PING request. Send PONG response.
                            socket.SendTo(IDTPacket.CreateFromString("$$PONG$$").GetBytes(), udpEndPoint);
                        }
                        else if (packet.GetString() == "$$PONG$$")
                        {
                            // Not enqueue PONG response. Do nothing.
                        }
                        else
                        {
                            // Enqueue message
                            _messageQueue.Enqueue(new IDTMessage(udpEndPoint!, socket, packet));
                            packetCount++;
                        }
                    }

                    // Move pointer to next packet.
                    offset += 4 + packetLength;
                }

                // Copies remaining bytes to the beginning of the buffer.
                if (offset <= (bytesRead + remainingBufferBytes))
                {
                    Buffer.BlockCopy(buffer, offset, buffer, 0, (bytesRead + remainingBufferBytes) - offset);

                    remainingBufferBytes = (bytesRead + remainingBufferBytes) - offset;
                }

                return packetCount;
            }
            return packetCount;
        }
        catch
        {
            throw;
        }
    }

}