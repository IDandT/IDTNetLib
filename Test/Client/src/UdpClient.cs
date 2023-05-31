using System.Diagnostics;
using IDTNetLib;

namespace ClientTest
{
    public class UDPClient
    {

        // Event executed when client is connected
        public static void OnConnectedClient(object? sender, IDTSocket socket)
        {
            Console.WriteLine("Client connected to: {0} ", socket.RemoteEndPoint);
        }

        // Event executed when client is disconnected
        public static void OnDisconnectedClient(object? sender, IDTSocket socket)
        {
            Console.WriteLine("Client disconnected from: {0} ", socket.RemoteEndPoint);
        }

        // Event executed when a message is received
        public static void OnMessageReceived(object? sender, IDTMessage message)
        {
            string sourceEP = message.RemoteEndPoint!.ToString() ?? "<unknown>";
            string textMessage = message.Packet.GetString();

            Console.WriteLine("Message received from {0}: \"{1}\"", sourceEP, textMessage);
        }


        // Run UDP Client test
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
                IDTUdpClient client = new IDTUdpClient("127.0.0.1", 50000);

                // Configure event handlers
                client.OnConnect += OnConnectedClient;
                client.OnDisconnect += OnDisconnectedClient;
                client.OnProcessMsg += OnMessageReceived;


                // Connect to server
                client.Connect();


                // Send messages to server until user press enter
                while (true)
                {
                    string input = Console.ReadLine() ?? "";

                    if (input == "") break;

                    try
                    {
                        client.SendTo(IDTPacket.CreateFromString(input));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("CLIENT ERROR: {0}", e.Message);
                    }
                }


                // Disconnect from server
                client.Disconnect();


                Console.WriteLine("Client stopped");

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }
    }
}
