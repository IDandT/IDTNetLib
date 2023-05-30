namespace ClientTest
{
    public class Program
    {
        public static void Main()
        {
            Console.InputEncoding = System.Text.Encoding.Unicode;

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