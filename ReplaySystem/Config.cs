namespace ReplaySystem
{
    using System.ComponentModel;
    using Exiled.API.Interfaces;

    public class Config : IConfig
    {
        [Description("Enable or disable the ReplaySystem plugin.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable detailed debug logs.")]
        public bool Debug { get; set; } = false;

        [Description("Folder where replay files are stored (relative to Exiled Configs if not absolute).")]
        public string ReplayDirectory { get; set; } = "Replays";

        [Description("Shown when a player lacks the replaysystem.use permission.")]
        public string MsgPermissionDenied { get; set; } = "<color=#ff5555>Permission denied. Required: replaysystem.use</color>";

        [Description("Shown when 'replaysystem' is typed without a sub-command.")]
        public string MsgUsageRoot { get; set; } = "Usage: replaysystem <available|schedule|cancel>";

        [Description("Shown when 'replaysystem schedule' is typed without an id.")]
        public string MsgUsageSchedule { get; set; } = "Usage: replaysystem schedule <id>  (id 1-5, from 'replaysystem available')";

        [Description("Shown when no replay files are found in the directory.")]
        public string MsgNoReplays { get; set; } = "No replays available.";

        [Description("Header of the 'replaysystem available' list. {0}=count.")]
        public string MsgAvailableHeader { get; set; } = "Available replays ({0}):";

        [Description("Format of one line in the list. {0}=id 1-5, {1}=server, {2}=start, {3}=end, {4}=players, {5}=shortId, {6}=seed, {7}=sizeKB.")]
        public string MsgAvailableLine { get; set; } = "  [{0}] {1} — {2:yyyy-MM-dd HH:mm:ss} → {3:HH:mm:ss} — {4} players — id {5} — seed {6} — {7} KB";

        [Description("Shown when the requested replay id does not exist. {0}=id, {1}=available count.")]
        public string MsgReplayOutOfRange { get; set; } = "Replay id {0} out of range (only {1} available).";

        [Description("Shown when a replay is successfully scheduled. {0}=id 1-5, {1}=file, {2}=seed.")]
        public string MsgScheduled { get; set; } = "Replay [{0}] '{1}' scheduled for the next round (seed {2} forced).";

        [Description("Shown when no replay is currently scheduled.")]
        public string MsgNoScheduled { get; set; } = "No replay scheduled.";

        [Description("Shown when the scheduled replay is cancelled.")]
        public string MsgCancelled { get; set; } = "Scheduled replay cancelled.";

        [Description("Broadcast shown at the start of a dedicated replay round. {0}=server, {1}=date, {2}=players, {3}=duration (s).")]
        public string MsgReplayBroadcast { get; set; } =
            "<color=#ffaa00><b>REPLAY MODE</b></color>\n<size=22>{0} — {1:yyyy-MM-dd HH:mm} — {2} players — {3}s</size>";

        [Description("Duration in seconds of the replay-start broadcast.")]
        public ushort ReplayBroadcastSeconds { get; set; } = 8;
    }
}
