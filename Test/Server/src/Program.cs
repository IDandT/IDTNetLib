namespace ServerTest
{
    public class Program
    {
        public static void Main()
        {
            Console.InputEncoding = System.Text.Encoding.Unicode;

            try
            {
                // TCPServer.Test();
                // UDPServer.Test();
                WsServer.Test();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            };
        }
    }
}