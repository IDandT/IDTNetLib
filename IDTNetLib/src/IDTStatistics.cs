namespace IDTNetLib;

/// <summary>
/// Stores statistics information for client and server.
////    It has multiple indicators and some methods to print info.
/// </summary>
public class IDTStatistics
{
    public int BytesReceived { get; set; }
    public int BytesSent { get; set; }
    public int PacketsReceived { get; set; }
    public int PacketsSent { get; set; }
    public int ReceiveOperations { get; set; }
    public int SendOperations { get; set; }
    public int ProcessedMessages { get; set; }
    public int ConnectionsAccepted { get; set; }
    public int ConnectionsRejected { get; set; }
    public int ConnectionsDropped { get; set; }
    public int ConnectionsPeak { get; set; }

    // Constructor. Creates a new instance of IDTStatistics.
    public IDTStatistics()
    {
        BytesReceived = 0;
        BytesSent = 0;
        PacketsReceived = 0;
        PacketsSent = 0;
        ReceiveOperations = 0;
        SendOperations = 0;
        ProcessedMessages = 0;
        ConnectionsAccepted = 0;
        ConnectionsRejected = 0;
        ConnectionsDropped = 0;
        ConnectionsPeak = 0;
    }


    // Reinitializes the statistics.
    public void Reset()
    {
        BytesReceived = 0;
        BytesSent = 0;
        PacketsReceived = 0;
        PacketsSent = 0;
        ReceiveOperations = 0;
        SendOperations = 0;
        ConnectionsAccepted = 0;
        ConnectionsRejected = 0;
        ConnectionsDropped = 0;
        ConnectionsPeak = 0;
    }


    // Special WriteLine starting at indicated row (no clear screen needed, no blinking).
    private static void WriteLineAt(int row, string text)
    {
        const int numCols = 80;
        Console.SetCursorPosition(0, row);
        Console.WriteLine(text.PadRight(numCols));
    }


    // Prints the statistics at the indicated row (no clear screen needed, no blinking).
    public void PrintStatisticsAtRow(string header, int row)
    {

        WriteLineAt(row, $"INBOUND OPERATIONS ({header})");
        WriteLineAt(row + 1, $"------------------");
        WriteLineAt(row + 2, $"Receive operations:      {ReceiveOperations:#,##0}");
        WriteLineAt(row + 3, $"Packets received:        {PacketsReceived:#,##0}");
        WriteLineAt(row + 4, $"Bytes received:          {BytesReceived:#,##0}  ({(float)BytesReceived / 1024 / 1024:#,##0.00} MB)");
        WriteLineAt(row + 5, $"");
        WriteLineAt(row + 6, $"OUTBOUND OPERATIONS");
        WriteLineAt(row + 7, $"-------------------");
        WriteLineAt(row + 8, $"Send operations:         {SendOperations:#,##0}");
        WriteLineAt(row + 9, $"Packets sent:            {PacketsSent:#,##0}");
        WriteLineAt(row + 10, $"Bytes sent:              {BytesSent:#,##0}  ({(float)BytesSent / 1024 / 1024:#,##0.00} MB)");
        WriteLineAt(row + 11, $"");
        WriteLineAt(row + 12, $"PROCESSED MESSAGES");
        WriteLineAt(row + 13, $"------------------");
        WriteLineAt(row + 14, $"Processed messages:      {ProcessedMessages:#,##0}");
        WriteLineAt(row + 15, $"");
        WriteLineAt(row + 15, $"");
        WriteLineAt(row + 16, $"CONNECTIONS");
        WriteLineAt(row + 17, $"-----------");
        WriteLineAt(row + 18, $"Connections accepted:    {ConnectionsAccepted:#,##0}");
        WriteLineAt(row + 19, $"Connections rejected:    {ConnectionsRejected:#,##0}");
        WriteLineAt(row + 20, $"Connections dropped:     {ConnectionsDropped:#,##0}");
        WriteLineAt(row + 21, $"Connections peak:        {ConnectionsPeak:#,##0}");
        WriteLineAt(row + 22, $"");
    }


    //  Prints the statistic.
    public void PrintStatistics()
    {
        Console.WriteLine($"INBOUND OPERATIONS");
        Console.WriteLine($"------------------");
        Console.WriteLine($"Receive operations:      {ReceiveOperations:#,##0}");
        Console.WriteLine($"Packets received:        {PacketsReceived:#,##0}");
        Console.WriteLine($"Bytes received:          {BytesReceived:#,##0}  ({(float)BytesReceived / 1024 / 1024:#,##0.00} MB)");
        Console.WriteLine();
        Console.WriteLine($"OUTBOUND OPERATIONS");
        Console.WriteLine($"-------------------");
        Console.WriteLine($"Send operations:         {SendOperations:#,##0}");
        Console.WriteLine($"Packets sent:            {PacketsSent:#,##0}");
        Console.WriteLine($"Bytes sent:              {BytesSent:#,##0}  ({(float)BytesSent / 1024 / 1024:#,##0.00} MB)");
        Console.WriteLine();
        Console.WriteLine($"PROCESSED MESSAGES");
        Console.WriteLine($"------------------");
        Console.WriteLine($"Processed messages:      {ProcessedMessages:#,##0}");
        Console.WriteLine();
        Console.WriteLine($"CONNECTIONS");
        Console.WriteLine($"-----------");
        Console.WriteLine($"Connections accepted:    {ConnectionsAccepted:#,##0}");
        Console.WriteLine($"Connections rejected:    {ConnectionsRejected:#,##0}");
        Console.WriteLine($"Connections dropped:     {ConnectionsDropped:#,##0}");
        Console.WriteLine($"Connections peak:        {ConnectionsPeak:#,##0}");
        Console.WriteLine();
    }
}




