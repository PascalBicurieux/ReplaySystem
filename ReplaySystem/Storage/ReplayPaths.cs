namespace ReplaySystem.Storage
{
    using System;
    using System.IO;
    using System.Text;
    using Exiled.API.Features;

    public static class ReplayPaths
    {
        public static string ReplayDirectory
        {
            get
            {
                string configured = Plugin.Instance?.Config?.ReplayDirectory ?? "Replays";
                string path = Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(Paths.Configs, "ReplaySystem", configured);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string ArchiveZip => Path.Combine(ReplayDirectory, "archive.zip");

        public static string BuildReplayFileName(string serverName, DateTime startedUtc, DateTime endedUtc, int playerCount, string roundId)
        {
            string sanitized = SanitizeForFileName(serverName);
            if (sanitized.Length > 32) sanitized = sanitized.Substring(0, 32);
            string shortId = (roundId ?? "00000000").Substring(0, Math.Min(8, (roundId ?? "").Length));
            return $"{sanitized}_{startedUtc:yyyy-MM-dd_HH-mm-ss}_{endedUtc:HH-mm-ss}_{playerCount}p_{shortId}.replay.gz";
        }

        private static string SanitizeForFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Server";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    sb.Append(c);
                else if (char.IsWhiteSpace(c))
                    sb.Append('-');
            }
            return sb.Length == 0 ? "Server" : sb.ToString();
        }
    }
}
