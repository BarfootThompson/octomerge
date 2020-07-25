namespace OctoMerge
{
    class Command
    {
        public string VariableFile { get; set; }
        public string TemplateFile { get; set; }
        public string ResultFile { get; set; }
        public bool SuppressWarnings { get; set; }
        public bool WarningsAsErrors { get; set; }
        public bool AllowPartialTemplates { get; set; }
        public bool Verbose { get; set; }
        public bool Multifile { get; set; }
        public bool DumpResponseOnErrors { get; set; }
        public bool WarnAboutGlobals { get; set; }
    }
}
