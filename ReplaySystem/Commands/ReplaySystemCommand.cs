namespace ReplaySystem.Commands
{
    using System;
    using CommandSystem;
    using Exiled.Permissions.Extensions;
    using ReplaySystem.Storage;
    using RemoteAdmin;

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class ReplaySystemCommand : ParentCommand
    {
        public ReplaySystemCommand() => LoadGeneratedCommands();

        public override string Command => "replaysystem";
        public override string[] Aliases => new[] { "rsys" };
        public override string Description => "Round replay system — available / schedule / cancel.";

        public override void LoadGeneratedCommands()
        {
            RegisterCommand(new AvailableSubCommand());
            RegisterCommand(new ScheduleSubCommand());
            RegisterCommand(new CancelSubCommand());
        }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Permission.Check(sender, out response)) return false;
            response = Plugin.Instance.Config.MsgUsageRoot;
            return false;
        }
    }

    internal static class Permission
    {
        public const string Node = "replaysystem.use";

        public static bool Check(ICommandSender sender, out string denyResponse)
        {
            if (sender.CheckPermission(Node))
            {
                denyResponse = null;
                return true;
            }
            denyResponse = Plugin.Instance.Config.MsgPermissionDenied;
            return false;
        }
    }

    public class AvailableSubCommand : ICommand
    {
        public string Command => "available";
        public string[] Aliases => new[] { "availables", "list", "ls" };
        public string Description => "List the most recent replays.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Permission.Check(sender, out response)) return false;

            var list = ReplayStore.ListAvailable();
            if (list.Count == 0)
            {
                response = Plugin.Instance.Config.MsgNoReplays;
                return true;
            }

            var cfg = Plugin.Instance.Config;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format(cfg.MsgAvailableHeader, list.Count));
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var b = e.Bundle;
                string shortId = (b.RoundId ?? string.Empty).Length >= 8 ? b.RoundId.Substring(0, 8) : (b.RoundId ?? "?");
                sb.AppendLine(string.Format(
                    cfg.MsgAvailableLine,
                    i + 1,
                    b.ServerName ?? "?",
                    b.StartedUtc,
                    b.EndedUtc,
                    b.Players.Count,
                    shortId,
                    b.MapSeed,
                    e.FileSize / 1024));
            }
            response = sb.ToString();
            return true;
        }
    }

    public class ScheduleSubCommand : ICommand
    {
        public string Command => "schedule";
        public string[] Aliases => new[] { "queue" };
        public string Description => "schedule <id> — queue replay for the next round.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Permission.Check(sender, out response)) return false;

            var cfg = Plugin.Instance.Config;
            if (arguments.Count == 0 || !int.TryParse(arguments.At(0), out int id) || id < 1)
            {
                response = cfg.MsgUsageSchedule;
                return false;
            }

            var list = ReplayStore.ListAvailable();
            if (id > list.Count)
            {
                response = string.Format(cfg.MsgReplayOutOfRange, id, list.Count);
                return false;
            }

            var entry = list[id - 1];
            Plugin.Instance.ScheduleReplay(entry.Bundle, entry.FileName);
            response = string.Format(cfg.MsgScheduled, id, entry.FileName, entry.Bundle.MapSeed);
            return true;
        }
    }

    public class CancelSubCommand : ICommand
    {
        public string Command => "cancel";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Cancel the scheduled replay.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!Permission.Check(sender, out response)) return false;

            var cfg = Plugin.Instance.Config;
            if (Plugin.Instance.ScheduledReplay == null)
            {
                response = cfg.MsgNoScheduled;
                return false;
            }
            Plugin.Instance.CancelScheduledReplay();
            response = cfg.MsgCancelled;
            return true;
        }
    }
}
