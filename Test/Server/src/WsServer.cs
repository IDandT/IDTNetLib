using System.Diagnostics;
using IDTNetLib;

namespace ServerTest;


public class WsServer
{
    // Event executed when new client connects to server.
    public static void OnConnectClient(object? sender, IDTSocket newClient)
    {
        Console.WriteLine("Client connected from {0}", newClient.RemoteEndPoint);
    }

    // Event executed when client is disconnected from server.
    public static void OnDisconnectClient(object? sender, IDTSocket client)
    {
        Console.WriteLine("Client disconnected from {0}", client.RemoteEndPoint);
    }

    // Event executed when a message is received.
    public static void OnMessageReceived(object? sender, IDTMessage message)
    {
        try
        {
            // Get message received.
            string textMessage = message.Packet.GetString();
            string sourceEP = message.RemoteEndPoint!.ToString() ?? "<unknown>";

            Console.WriteLine("Message received from {0}: \"{1}\".  Acknowledge sent.", sourceEP, textMessage);

            // Response to remote host.
            IDTWebSocketServer server = (IDTWebSocketServer)sender!;
            IDTSocket socket = message.SourceSocket!;
            IDTPacket responsePacket = IDTPacket.CreateFromString($"ACK: {textMessage}");

            if (socket.Connected) server.Send(socket, responsePacket);
        }
        catch (Exception e)
        {
            Console.WriteLine("SERVER ERROR: {0}", e.Message);
        }
    }


    // Run TCP Server test.
    public static void Test()
    {
        Console.Clear();
        Console.WriteLine("Running server...");

        try
        {
            // Set position and size of console window, useful for organize client/server execution.
            IDTUtils.SetWindowPos(Process.GetCurrentProcess().MainWindowHandle, 0, 0, 580, 600);

            // Create server object with some params.
            IDTWebSocketServer server = new IDTWebSocketServer("127.0.0.1", 80);

            server.MaximumClients = 4;

            // // Configure event handlers.
            server.OnConnect += OnConnectClient;
            server.OnDisconnect += OnDisconnectClient;
            server.OnProcessMsg += OnMessageReceived;

            // Start listening for clients.
            server.Start();


            // Commands for start, stop, quit and get server status.
            Console.WriteLine("Commands:");
            Console.WriteLine("start   -  Start server.");
            Console.WriteLine("stop    -  Stop server.");
            Console.WriteLine("status  -  Get server status.");
            Console.WriteLine("quit    -  Close server.");


            bool quit = false;

            while (!quit)
            {
                string input = Console.ReadLine() ?? "";

                switch (input.ToLower())
                {
                    case "start":

                        if (!server.IsRunning) server.Start();
                        Console.WriteLine("Server started.");
                        break;

                    case "stop":

                        if (server.IsRunning) server.Stop();
                        Console.WriteLine("Server stopped.");
                        break;

                    case "status":

                        Console.WriteLine("Server status: {0}", server.IsRunning ? "Running" : "Stopped");
                        break;

                    case "quit":

                        quit = true;
                        return;

                    default:
                        break;
                }
            }

            // Stop server and end test.
            if (server.IsRunning) server.Stop();


            Console.WriteLine("Server closed. Press any key.");
            Console.ReadKey();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            Console.ReadKey();
        };
    }
}
