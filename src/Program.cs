using System;

namespace OctoMerge
{
    static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    CommandLineParser.Usage();
                    return 0;
                }

                Processor.Command = CommandLineParser.Parse(args);
                Processor.Run();
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error: {e}");
                return 255;
            }
        }
    }
}
