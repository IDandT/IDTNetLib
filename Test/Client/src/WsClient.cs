using System.Diagnostics;
using IDTNetLib;

namespace ClientTest;


public class WsClient
{
    // Event executed when client is connected
    public static void OnConnectedClient(object? sender, IDTWebSocket socket)
    {
        Console.WriteLine("Client connected to: {0} ", socket.Uri);
    }

    // Event executed when client is disconnected
    public static void OnDisconnectedClient(object? sender, IDTWebSocket socket)
    {
        Console.WriteLine("Client disconnected from: {0} ", socket.Uri);
    }

    // Event executed when a message is received
    public static void OnMessageReceived(object? sender, IDTMessage message)
    {
        string remoteUri = message.RemoteUri!.ToString() ?? "<unknown>";
        string textMessage = message.Packet.GetString();

        if (textMessage != string.Empty) Console.WriteLine("Message received from {0}: \"{1}\"", remoteUri, textMessage);
    }

    // Run TCP Client test
    public static void Test()
    {
        Console.Clear();
        Console.WriteLine("Running client...");

        try
        {

            // Set position and size of console window, useful for organize client/server execution
            IDTUtils.SetWindowPos(Process.GetCurrentProcess().MainWindowHandle, 590, 0, 580, 600);

            // Wait one moment to ensure server to start
            Thread.Sleep(1000);

            // Create client object with some params
            // IDTWebSocketClient client = new IDTWebSocketClient("wss://ws.postman-echo.com/raw");
            IDTWebSocketClient client = new IDTWebSocketClient("ws://127.0.0.1:80");

            // Configure event handlers
            client.OnConnect += OnConnectedClient;
            client.OnDisconnect += OnDisconnectedClient;
            client.OnProcessMsg += OnMessageReceived;


            client.Connect();


            // Send messages to server until user press enter
            while (true)
            {
                string input = Console.ReadLine() ?? "";

                if (input == "") break;

                try
                {
                    client.Send(IDTPacket.CreateFromString(input));
                }
                catch (Exception e)
                {
                    Console.WriteLine("CLIENT ERROR: {0}", e.Message);
                }
            }


            client.Disconnect();


            Console.WriteLine("Client closed...");

            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.ReadKey();
        }
    }
}


