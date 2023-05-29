using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;

namespace IDTNetLib;


/// <summary>
/// Utility functions for IDTNetLib.
/// Some used for development, others for final user.
/// </summary>
public class IDTUtils
{

    // External reference for p/invoke function.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, UInt32 uFlags);


    // Set console window posticion and size.
    public static void SetWindowPos(IntPtr hWnd, int x, int y, int width, int height)
    {
        IDTUtils.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            x, y, width, height,
            Convert.ToUInt32(IDTUtils.SWP.NOZORDER | IDTUtils.SWP.SHOWWINDOW)
        );
    }


    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public static readonly IntPtr HWND_TOP = new IntPtr(0);
    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);


    /// SetWindowPos Flags
    public static class SWP
    {
        public static readonly int
        NOSIZE = 0x0001,
        NOMOVE = 0x0002,
        NOZORDER = 0x0004,
        NOREDRAW = 0x0008,
        NOACTIVATE = 0x0010,
        DRAWFRAME = 0x0020,
        FRAMECHANGED = 0x0020,
        SHOWWINDOW = 0x0040,
        HIDEWINDOW = 0x0080,
        NOCOPYBITS = 0x0100,
        NOOWNERZORDER = 0x0200,
        NOREPOSITION = 0x0200,
        NOSENDCHANGING = 0x0400,
        DEFERERASE = 0x2000,
        ASYNCWINDOWPOS = 0x4000;
    }


    /// Window handles (HWND) used for hWndInsertAfter
    public static class HWND
    {
        public static readonly IntPtr
        NoTopMost = new IntPtr(-2),
        TopMost = new IntPtr(-1),
        Top = new IntPtr(0),
        Bottom = new IntPtr(1);
    }


    // Concatenate two byte arrays.
    public static byte[] ConcatByteArrays(byte[] first, byte[] second, int byteCount)
    {
        byte[] bytes = new byte[first.Length + byteCount];
        Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
        Buffer.BlockCopy(second, 0, bytes, first.Length, byteCount);
        return bytes;
    }


    // Convert object to byte array.
    public static byte[] SerializeObject(object obj)
    {
        MemoryStream stream = new MemoryStream();
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Serialize(stream, obj);
        return stream.ToArray();
    }


    // Convert byte array to object.
    public static object DeserializeObject(byte[] bytes)
    {
        MemoryStream stream = new MemoryStream(bytes);
        BinaryFormatter formatter = new BinaryFormatter();
        return formatter.Deserialize(stream);
    }


    // Sleep some milliseconds when counter arrives to a specified cycle count.
    // Useful for avoid traffic congestion without long sleeps.
    public static void SleepEveryCycleCount(int counter, int cycles, int milliseconds)
    {
        if (counter % cycles == 0)
        {
            Thread.Sleep(milliseconds);
        }
    }

    // Print pretty formatted segment data from buffer. For debug porpuses.
    public static void PrintBuffer(byte[] buffer, int offset = 0, int count = 0)
    {
        if (count == 0) count = buffer.Length;

        Console.WriteLine(new string('_', buffer.Length * 4));
        for (int i = offset; i < count; i++)
        {
            string pos = $"   {i}|";
            string header = pos.Substring(pos.Length - 4, 4);
            Console.Write(header);
        }
        Console.WriteLine();
        Console.WriteLine(new string('-', buffer.Length * 4));
        for (int i = offset; i < count; i++)
        {
            Console.Write($"  {(char)buffer[i]} ");
        }
        Console.WriteLine();
        Console.WriteLine();
    }
}

