namespace ClientTest
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                // TCPCLient.Test();
                // UDPClient.Test();
                WsClient.Test();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            };
        }
    }
}