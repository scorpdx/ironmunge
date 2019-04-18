using System;
using System.IO;
using System.Linq;

namespace ironmunge.Common
{
    public static class GitHelpers
    {
        public static string DefaultGitPath
            => Directory.EnumerateFiles("./Resources/git/cmd/", "git*", SearchOption.TopDirectoryOnly).SingleOrDefault();
    }
}
