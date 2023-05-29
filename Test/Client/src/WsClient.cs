using System.Diagnostics;
using IDTNetLib;

namespace ClientTest;

public class WsClient
{
    // // Event executed when client is connected
    // public static void OnConnectedClient(object? sender, IDTSocket socket)
    // {
    //     Console.WriteLine("Client connected to: {0} ", socket.RemoteEndPoint);
    // }

    // // Event executed when client is disconnected
    // public static void OnDisconnectedClient(object? sender, IDTSocket socket)
    // {
    //     Console.WriteLine("Client disconnected from: {0} ", socket.RemoteEndPoint);
    // }

    // Event executed when a message is received
    public static void OnMessageReceived(object? sender, IDTMessage message)
    {
        string remoteUri = message.RemoteUri!.ToString() ?? "<unknown>";
        string textMessage = message.Packet.GetString();

        if (textMessage != string.Empty)
            Console.WriteLine("Message received from {0}: \"{1}\"", remoteUri, textMessage);
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
            IDTWebSocketClient client = new IDTWebSocketClient("wss://ws.postman-echo.com/raw");

            // Configure event handlers
            // client.OnConnect += OnConnectedClient;
            // client.OnDisconnect += OnDisconnectedClient;
            client.OnProcessMsg += OnMessageReceived;


            client.Connect();


            // Send messages to server until user press enter
            string input = "";
            do
            {
                input = Console.ReadLine() ?? "";

                if (input != "")
                {
                    client.Send(IDTPacket.CreateFromString(input));
                }

            } while (input != "");


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


