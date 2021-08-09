using CommandLine;

namespace ironmunge
{
    public class IronmungeOptions : Ironmunge.Common.Options
    {
        [Option('n', "notifications", HelpText = "Play notification sounds while saving history", Default = true)]
        public bool Notifications { get; set; }

        [Option('r', "remote", HelpText = "Remote server to push updates")]
        public string Remote { get; set; }
    }
}
