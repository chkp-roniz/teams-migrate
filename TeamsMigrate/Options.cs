using CommandLine;

namespace TeamsMigrate
{
    class Options
    {
        [Option('n', "name", Required = false, Default = "", HelpText = "Team name")]
        public string TeamsName { get; set; }

        [Option('m', "messages", Required = false, Default = true, HelpText = "Migrate channel messages")]
        public bool MigrateMessages { get; set; }

        [Option('f', "files", Required = false, Default = true, HelpText = "Migrate channel files")]
        public bool MigrateFiles { get; set; }

        [Option('e', "export", Required = true, HelpText = "Export file path")]
        public string ExportPath { get; set; }

        [Option('c', "client", Required = true, HelpText = "Application (client) ID")]
        public string ClientId { get; set; }

        [Option('t', "tenant", Required = true, HelpText = "Directory (tenant) ID")]
        public string TenantId { get; set; }

        [Option('r', "redirect", Required = true, HelpText = "Redirect URI")]
        public string AadRedirectUri { get; set; }

        [Option('a', "authority", Required = false, Default = "https://login.microsoftonline.com/{0}", HelpText = "Authentication authority URL")]
        public string AadInstance { get; set; }

        [Option('d', "domain", Required = true, HelpText = "Domain")]
        public string Domain { get; set; }

        [Option('v', "verbose", Required = false, Default = false, HelpText = "Verbose")]
        public bool Verbose { get; set; }

        [Option('o', "readonly", Required = false, Default = false, HelpText = "Readonly mode")]
        public bool ReadOnly { get; set; }

        [Option('s', "secret", Required = true, HelpText = "Client Secret token")]
        public string ClientSecret { get; set; }

        [Option('u', "users", Required = false, Default = false, HelpText = "Create or restore missing users")]
        public bool CreateMissingUsers { get; set; }
    }
}
