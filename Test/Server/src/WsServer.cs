using System.Diagnostics;
using System.Text;
using IDTNetLib;

namespace ServerTest
{
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


                // Server only attends client messages through event handlers.
                // This is just to keep server alive. Does nothing else...
                while (true)
                {
                    Thread.Sleep(5000);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            };
        }
    }
}