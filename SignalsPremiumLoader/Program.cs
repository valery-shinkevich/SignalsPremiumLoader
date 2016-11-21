using System;

namespace SignalsPremiumLoader
{
    internal class Program
    {
        private static void Main()
        {
            try
            {
                var worker = new Worker();
                worker.DoGetAndParseAsync("...@mail...", "...password...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                Console.WriteLine("Press any key...");
                Console.ReadKey();
                return;
            }
            while (Console.ReadKey().Key != ConsoleKey.Escape)
            {
            }
            Environment.Exit(0);
        }
    }
}