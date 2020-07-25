using System;
using System.IO;
using System.Linq;

namespace OctoMerge
{
    static class CommandLineParser
    {
        public static Command Parse(string[] args)
        {
            Command result = new Command();
            args = args.Select(x => x.Trim()).ToArray();
            var flags = args.Where(x => x.StartsWith("-")).ToArray();
            var @params = args.Where(x => !(x.StartsWith("-")) && !string.IsNullOrWhiteSpace(x)).ToArray();
            foreach (var flag in flags)
            {
                var val = flag.Substring(1);
                if (val.Length == 0)
                {
                    Console.WriteLine($"Error: Empty flags: {flag}");
                    Usage();
                    Environment.Exit(1);
                }
                foreach (char c in val)
                {
                    switch (c)
                    {
                        case 'q':
                            result.SuppressWarnings = true;
                            break;
                        case 'p':
                            result.AllowPartialTemplates = true;
                            break;
                        case 's':
                            result.WarningsAsErrors = true;
                            break;
                        case 'v':
                            result.Verbose = true;
                            break;
                        case 'x':
                            result.Multifile = true;
                            break;
                        case 'd':
                            result.DumpResponseOnErrors = true;
                            break;
                        case 'g':
                            result.WarnAboutGlobals = true;
                            break;
                        default:
                            Console.WriteLine($"Error: Unknown flag: {c} in {flag}");
                            Usage();
                            Environment.Exit(1);
                            break;
                    }                    
                }
            }

            if (@params.Length != 3 && !result.Multifile)
            {
                Console.WriteLine($"Error: Expecting 3 main parameters, got {@params.Length}");
                Usage();
                Environment.Exit(1);
            }

            if (@params.Length != 1 && result.Multifile)
            {
                Console.WriteLine($"Error: Expecting 1 main parameters, got {@params.Length}");
                Usage();
                Environment.Exit(1);
            }

            if (!result.Multifile && result.WarnAboutGlobals)
            {
                Console.WriteLine($"Error: -g flag specified but -x flag is not specified.");
                Usage();
                Environment.Exit(1);
            }

            result.VariableFile = @params[0];
            if (@params.Length > 1)
            {
                result.TemplateFile = @params[1];
                result.ResultFile = @params[2];
            }

            if (!File.Exists(result.VariableFile))
            {
                Console.WriteLine($"Error: File {result.VariableFile} does not exist");
                Environment.Exit(1);
            }
            
            if (@params.Length > 1 && !File.Exists(result.TemplateFile))
            {
                Console.WriteLine($"Error: File {result.TemplateFile} does not exist");
                Environment.Exit(1);
            }

            return result;
        }

        public static void Usage()
        {
            string version = typeof(CommandLineParser).Assembly.GetName().Version.ToString();
            Console.WriteLine($"OctoMerge v{version}");
            Console.WriteLine("Usage: OctoMerge variables.toml template.txt result.txt [-q] [-p] [-s] [-v] [-d]");
            Console.WriteLine("   Or: OctoMerge variables.toml -x [-q] [-g] [-p] [-s] [-v] [-d]");
            Console.WriteLine("  Merges Octopus variables from variables toml file into template and writes out the result file");
            Console.WriteLine("  -q - do not warn, when a variable from toml not used in the template (and the other way around, if -p is also specified)");
            Console.WriteLine("  -g - in multifile mode also warn if a global was not used in a template");
            Console.WriteLine("  -s - treat warnings (described above) as errors, and terminate");
            Console.WriteLine("  -p - do not terminate with a error if there are some variables in the template that do not have correspondent variables in the variables toml file)");
            Console.WriteLine("  -v - print out all variable names found in the variables toml file and in the template");
            Console.WriteLine("  -d - in case of a vault error, dump vault response to console. WARNING! - could contain secrets");
            Console.WriteLine();
            Console.WriteLine("  -x - Multifile mode. variables.toml expects tables with names corresponding to result filenames");
            Console.WriteLine("       For example:");
            Console.WriteLine("           [\"deployment.yaml\"]    ");
            Console.WriteLine("           key = \"value\"");
            Console.WriteLine("       Will attempt to convert deployment.yaml.template into deployment.yaml");
        }
    }
}
