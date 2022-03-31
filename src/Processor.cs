using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Nett;
using Octostache;
using Octostache.Templates;

namespace OctoMerge
{
    static class Processor
    {
        class InputFile
        {
            public TomlTable Content;
            public string FileName;
        }

        public static Command Command;

        public static void Run()
        {
            TomlTable vars = MergeInput(ReadToml());
            CheckTypes(vars);
            if (Command.Multifile)
            {
                ProcessMultiple(vars);
            }
            else
            {
                ProcessFile(vars);
            }
        }

        private static TomlTable MergeInput(IList<InputFile> input)
        {
            TomlTable result = Toml.Create();
            foreach (InputFile file in input)
            {
                foreach (var var in file.Content)
                {
                    result.Add(var.Key, var.Value);
                }
            }

            return result;
        }

        private static IList<InputFile> ReadToml()
        {
            try
            {
                List<InputFile> result = new List<InputFile>();
                foreach(string variableFile in Command.VariableFiles)
                {
                    string template = File.ReadAllText(variableFile);
                    HashSet<string> templateVars = ParseTemplate(template, variableFile);
                    result.Add(new InputFile {Content = Toml.ReadFile(variableFile), FileName = variableFile });
                }
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: failed parsing variables toml file. {e.Message}");
                Environment.Exit(1);
                return null;
            }
        }

        private static void ProcessMultiple(TomlTable vars)
        {
            foreach (var var in vars)
            {
                if (var.Value.GetType() != typeof(TomlTable))
                {
                    continue;
                }

                Command.ResultFile = var.Key;
                Command.TemplateFile = $"{var.Key}.template";
                if (Command.Verbose)
                {
                    Console.WriteLine($"Verbose: Processing file:{Command.TemplateFile}");
                }
                ProcessFile(vars);
            }
        }

        private static string NormaliseFilepathAndSetCurrentDirectory(string variableFilepath)
        {
            var fi = new FileInfo(variableFilepath);
            Debug.Assert(fi.Directory != null, "fi.Directory != null");
            Environment.CurrentDirectory = fi.Directory.FullName;
            return fi.FullName;
        }

        private static void ProcessFile(TomlTable toml)
        {
            string templateFile = Command.TemplateFile.Replace('\\', Path.DirectorySeparatorChar);
            string resultFile = Command.ResultFile.Replace('\\', Path.DirectorySeparatorChar);
            if (!File.Exists(templateFile))
            {
                Console.WriteLine($"Error: File {templateFile} does not exist");
                Environment.Exit(1);
            }

            IList<Variable> vars = GetVariablesFromToml(toml, Command.Multifile, Command.ResultFile).ToList();
            Dictionary<string, Variable> lookup = vars.ToDictionary(x => x.Key);

            if (Command.Verbose)
            {
                LogVars(vars, null, true);
            }

            string template = File.ReadAllText(templateFile);
            HashSet<string> templateVars = ParseTemplate(template, templateFile);

            if (Command.Verbose)
            {
                LogVars(GetVariablesFromLookup(templateVars, lookup), templateFile, true);
            }

            ProcessUnusedVarsWarningsAndErrors(vars, templateVars, templateFile, lookup);
            VariableDictionary octoVars = SubstituteVaultValues(templateFile, vars);
            File.WriteAllText(resultFile, octoVars.Evaluate(template));
        }

        private static IEnumerable<Variable> GetVariablesFromToml(TomlTable toml, bool multifile, string resultFile)
        {
            IEnumerable<Variable> vars;
            if (multifile)
            {
                var inner = (TomlTable)toml.SingleOrDefault(x => x.Key == resultFile && x.Value.GetType() == typeof(TomlTable)).Value;
                var locals = inner.Where(x => (x.Value is TomlString)).Select(x => new Variable { Key = x.Key, Value = ((TomlString)x.Value).Value, Global = false });
                var globals = toml.Where(x => (x.Value is TomlString)).Select(x => new Variable { Key = x.Key, Value = ((TomlString)x.Value).Value, Global = true });
                var globalRefs = inner.Where(x => (x.Value is TomlBool) && ((TomlBool)x.Value).Value).Select(x => new Variable { Key = x.Key, GlobalReference = true });
                vars = globals.MergeWithOverwrite(locals).ToList();

                foreach(var globalRef in globalRefs)
                {
                    var v = vars.SingleOrDefault(x => x.Key == globalRef.Key);
                    if (v != null)
                    {
                        v.GlobalReference = true;
                    }
                    else
                    {
                        PrintWarning($"Template '{resultFile}' has '{globalRef.Key}' global reference, but there is no such global defined");
                    }
                }
            }
            else
            {
                vars = toml.Where(x => (x.Value is TomlString)).Select(x => new Variable { Key = x.Key, Value = ((TomlString)x.Value).Value, Global = false });
            }

            return vars;
        }

        private static IEnumerable<Variable> GetVariablesFromLookup(IEnumerable<string> templateVars, Dictionary<string, Variable> lookup)
        {
            return templateVars.Select(x => lookup.GetValueOrDefault(x) ?? new Variable { Key = x });
        }

        private static void LogVars(IEnumerable<Variable> vars, string filePath, bool isVerbose = false)
        {
            foreach (var var in vars)
            {
                string global = var.Global ? (var.GlobalReference ? " (global reference)" : " (global, no reference)") : "";
                string value = string.IsNullOrEmpty(var.Value) ? "" : $" = {var.Value}";
                string fileName = string.IsNullOrEmpty(filePath) || !isVerbose ? "" : $"{Path.GetFileName(filePath)}: ";
                string verbose = isVerbose ? "Verbose: " : "";
                Console.WriteLine($"{verbose}{fileName}{var.Key}{value}{global}");
            }
            Console.WriteLine();
        }

        private static HashSet<string> ParseTemplate(string template, string templateFileName)
        {
            TemplateParser.TryParseTemplate(template, out _, out var error);
            if (error != null)
            {
                Console.WriteLine($"Error: Could not parse template in file {templateFileName}. {error}");
                Environment.Exit(1);
            }
            return TemplateParser.ParseTemplateAndGetArgumentNames(template);
        }

        private static void ProcessUnusedVarsWarningsAndErrors(IList<Variable> vars, HashSet<string> templateVars, string templateFile, Dictionary<string, Variable> lookup)
        {
            var extraInVarsKeys = Command.WarnAboutGlobals ? vars.Select(x => x.Key) : vars.Where(x => !x.Global || x.GlobalReference).Select(x => x.Key);
            var extraInVars = extraInVarsKeys.Except(templateVars, StringComparer.InvariantCultureIgnoreCase).ToArray();
            var extraInTemplate = templateVars.Except(vars.Select(x => x.Key), StringComparer.InvariantCultureIgnoreCase).ToArray();

            var notGlobalRef = vars.Where(x => x.Global && !x.GlobalReference && templateVars.Contains(x.Key));
            if (notGlobalRef.Any())
            {
                Console.WriteLine($"Warning: In file {templateFile}");
                Console.WriteLine("Warning: these global variables are used in the template, but not marked as global refrenceses in toml:");
                LogVars(notGlobalRef, templateFile);

            }

            if (extraInTemplate.Length > 0 && (!Command.SuppressWarnings || !Command.AllowPartialTemplates))
            {
                string level = Command.AllowPartialTemplates ? "Warning" : "Error";
                Console.WriteLine($"{level}: In file {templateFile}");
                Console.WriteLine($"{level}: these variables are present in the template but not in the variables toml file:");
                LogVars(GetVariablesFromLookup(extraInTemplate, lookup), templateFile);
            }

            if (extraInVars.Length > 0 && !Command.SuppressWarnings)
            {
                Console.WriteLine($"Warning: In file {templateFile}");
                Console.WriteLine("Warning: these variables are present in the variables toml file but not in the template:");
                LogVars(GetVariablesFromLookup(extraInVars, lookup), templateFile);
            }

            if (extraInVars.Length > 0 && Command.WarningsAsErrors)
            {
                Console.WriteLine($"Error: In file {templateFile}");
                Console.WriteLine("Error: There are some global variables used that are not marked as global references and you specified to treat these as errors");
                Environment.Exit(1);
            }

            if (notGlobalRef.Any() && Command.WarningsAsErrors)
            {
                Console.WriteLine($"Error: In file {templateFile}");
                Console.WriteLine("Error: There are some variables present in varaible toml that is not in the template and you specified to treat these as errors");
                Environment.Exit(1);
            }

            if (extraInTemplate.Length > 0 && !Command.AllowPartialTemplates)
            {
                Console.WriteLine($"Error: In file {templateFile}");
                Console.WriteLine("Error: There are some variables in the template that cannot be substituted because they are missing in the variables toml file");
                Environment.Exit(1);
            }
        }

        private static VariableDictionary SubstituteVaultValues(string templateFile, IList<Variable> vars)
        {
            var octoVars = new VariableDictionary();
            foreach (var var in vars)
            {
                string value = var.Value;
                if (value.StartsWith("vault:"))
                {
                    if (Command.Verbose)
                    {
                        Console.WriteLine($"Verbose: {Path.GetFileName(templateFile)}: {var.Key} is a vault value");
                    }

                    value = Vault.GetVaultValue(Path.GetFileName(templateFile), var.Key, value, Command.DumpResponseOnErrors);
                }
                octoVars.Set(var.Key, value);
            }
            return octoVars;
        }

        private static void CheckTypes(TomlTable vars)
        {
            if (Command.Multifile)
            {
                foreach (var x in vars)
                {
                    switch (x.Value)
                    {
                        case TomlTable t:
                            ValidateTomlSection(t, x.Key);
                            break;
                        case TomlString _:
                            break;
                        default:
                            PrintWarning($"top-level element '{x.Key}' is of type '{x.Value.TomlType}'. Only tables (individual files in multifile mode) and string (globals) are exepected here");
                            break;
                    }
                }
            }
            else
            {
                ValidateTomlSection(vars);
            }
        }

        private static void ValidateTomlSection(TomlTable vars, string name = null)
        {
            foreach (var x in vars)
            {
                switch (x.Value)
                {
                    case TomlString _:
                        break;
                    case TomlBool tomlBool:
                        if (string.IsNullOrEmpty(name))
                        {
                            PrintWarning($"top-level element '{x.Key}' is of type '{x.Value.TomlType}'. Only strings (variables) are exepected here in single-file mode");
                        }
                        else
                        {
                            if (!tomlBool.Value)
                            {
                                PrintWarning($"Template '{name}' has '{x.Key}' set to 'false'. This key will be ignored");
                            }
                        }
                        break;
                    default:
                        if (string.IsNullOrEmpty(name))
                        {
                            PrintWarning($"top-level element '{x.Key}' is of type '{x.Value.TomlType}'. Only strings (variables) are exepected here in single-file mode");
                        }
                        else
                        {
                            PrintWarning($"Template '{name}' has '{x.Key}' of type '{x.Value.TomlType}'. Only strings (locals) and bools (global references) are expected here in multi-file mode");

                        }
                        break;
                }
            }
        }

        private static void PrintWarning(params string[] messages)
        {
            if (!Command.SuppressWarnings)
            {
                if (Command.WarningsAsErrors)
                {
                    foreach (string message in messages)
                    {
                        Console.WriteLine($"Error: {message}");
                    }
                    Environment.Exit(1);

                }
                else
                {
                    foreach (string message in messages)
                    {
                        Console.WriteLine($"Warning: {message}");
                    }
                }
            }
        }
    }
}
