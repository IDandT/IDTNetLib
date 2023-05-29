namespace ServerTest
{
    public class Program
    {
        public static void Main()
        {
            try
            {
                // TCPServer.Test();
                // UDPServer.Test();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            };
        }
    }
}