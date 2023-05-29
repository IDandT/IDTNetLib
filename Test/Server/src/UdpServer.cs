using System.Diagnostics;
using IDTNetLib;

namespace ServerTest
{
    public class UDPServer
    {

        // Event executed when new client connects to server
        public static void OnConnectClient(object? sender, UDPClientInfo newClient)
        {
            Console.WriteLine("Client connected from {0}", newClient.EndPoint);
        }

        // Event executed when client is disconnected from server
        public static void OnDisconnectClient(object? sender, UDPClientInfo client)
        {
            Console.WriteLine("Client disconnected from {0}", client.EndPoint);
        }

        // Event executed when a message is received
        public static void OnMessageReceived(object? sender, IDTMessage message)
        {
            // Get message received
            string textMessage = message.Packet.GetString();
            string sourceEP = message.RemoteEndPoint!.ToString() ?? "<unknown>";

            Console.WriteLine("Message received from {0}: \"{1}\". Acknowledge sent.", sourceEP, textMessage);

            // Response to remote host
            IDTUdpServer server = (IDTUdpServer)sender!;
            IDTPacket responsePacket = IDTPacket.CreateFromString($"ACK: {textMessage}");

            server.SendTo(responsePacket, message.RemoteEndPoint);
        }


        // Run UDP Server test
        public static void Test()
        {
            Console.Clear();
            Console.WriteLine("Running server...");

            try
            {
                // Set position and size of console window, useful for organize client/server execution
                IDTUtils.SetWindowPos(Process.GetCurrentProcess().MainWindowHandle, 0, 0, 580, 600);

                // Create server object with some params
                IDTUdpServer server = new IDTUdpServer("127.0.0.1", 50000);

                server.MaximumClients = 4;
                server.ClientCheckTiemout = 10;
                server.ClientRemoveTimeout = 20;

                // Configure event handlers
                server.OnConnect += OnConnectClient;
                server.OnDisconnect += OnDisconnectClient;
                server.OnProcessMsg += OnMessageReceived;

                // Start listening for clients
                server.Start();


                while (true)
                {
                    Thread.Sleep(1000);
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