using System.Text;

namespace IDTNetLib;


/// <summary>
/// Represents a packet of data. Its formed by:
///     - Header (4 bytes) that specifies the lenght of the body.
///     - Body itself.
/// </summary>
public class IDTPacket
{

    public int Lenght;
    public byte[]? Body;

    // Returns true if packet is filled (header len = body len)
    public bool Filled
    {
        get
        {
            if (Body == null) return false;
            return Lenght == Body.Length;
        }
    }

    // Creates empty packet.
    public IDTPacket()
    {
        Lenght = 0;
        Body = null;
    }


    // Creates packet with body specified.
    public IDTPacket(byte[] data)
    {

        Lenght = data.Length;
        Body = data;
    }


    // Return body byte buffer. Mostly used for socket send operations.
    public byte[] GetBytes()
    {
        if (Body == null) throw new NullReferenceException("Packet body is null, cannot get string from it.");
        byte[] len = BitConverter.GetBytes(Lenght);
        return IDTUtils.ConcatByteArrays(len, Body, Body.Length);
    }


    // Returns body decoded to string.
    public string GetString()
    {
        if (Body == null) throw new NullReferenceException("Packet body is null, cannot get string from it.");
        return Encoding.UTF8.GetString(Body);
    }


    // Returns a object from serialized packet.
    public object GetBinnary()
    {
        if (Body == null) throw new NullReferenceException("Packet body is null, cannot get string from it.");
        return IDTUtils.DeserializeObject(Body);
    }


    // Create new packet from a string. Mostly for send text messages.
    public static IDTPacket CreateFromString(string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        return new IDTPacket(body);
    }


    // Creates a new packet from serialized object.
    public static IDTPacket CreateFromObject(object obj)
    {
        byte[] body = IDTUtils.SerializeObject(obj);
        return new IDTPacket(body);
    }


    // Creates a new packet from a byte buffer.
    public static IDTPacket CreateFromBytes(byte[] bytes)
    {
        int lenght = BitConverter.ToInt32(bytes, 0);
        byte[] body = new byte[lenght];
        Buffer.BlockCopy(bytes, 4, body, 0, lenght);
        return new IDTPacket(body);
    }
}
