using System;
using System.Threading;
using clojureCLR_nrepl;

namespace clojureCLR_nrepl_cli
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  ClojureCLR nREPL Server");
            Console.WriteLine("========================================");
            Console.WriteLine();

            try
            {
                var host = Environment.GetEnvironmentVariable("NREPL_HOST") ?? "127.0.0.1";
                var portStr = Environment.GetEnvironmentVariable("NREPL_PORT") ?? "1667";
                var port = int.TryParse(portStr, out var p) ? p : 1667;

                var server = new NReplServer(host, port);
                server.Start();

                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
