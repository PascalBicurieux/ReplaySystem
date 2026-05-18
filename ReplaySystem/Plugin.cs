namespace ReplaySystem
{
    using System;
    using System.IO;
    using Exiled.API.Features;
    using HarmonyLib;
    using MEC;
    using PlayerRoles;
    using ReplaySystem.Playback;
    using ReplaySystem.Recording;
    using ReplaySystem.Seed;
    using ReplaySystem.Storage;
    using MapHandlers = Exiled.Events.Handlers.Map;
    using PlayerHandlers = Exiled.Events.Handlers.Player;
    using ServerHandlers = Exiled.Events.Handlers.Server;

    public class Plugin : Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "ReplaySystem";
        public override string Author => "jadbe";
        public override Version Version => new Version(0, 6, 0);
        public override Version RequiredExiledVersion => new Version(9, 13, 2);

        public SeedManager SeedManager { get; private set; }
        public DummyController DummyController { get; private set; }
        public MultiRecorder MultiRecorder { get; private set; }
        public ReplayPlayer ReplayPlayer { get; private set; }
        public MultiReplayPlayer MultiReplayPlayer { get; private set; }
        public ItemEventCapture ItemCapture { get; private set; }

        public ReplayBundle ScheduledReplay { get; private set; }
        public string ScheduledReplayFileName { get; private set; }

        private Harmony _harmony;

        public override void OnEnabled()
        {
            Instance = this;
            SeedManager = new SeedManager();
            DummyController = new DummyController();
            MultiRecorder = new MultiRecorder();
            ReplayPlayer = new ReplayPlayer();
            MultiReplayPlayer = new MultiReplayPlayer();
            ItemCapture = new ItemEventCapture(MultiRecorder);

            _harmony = new Harmony($"com.jadbe.replaysystem.{DateTime.UtcNow.Ticks}");
            _harmony.PatchAll();

            ItemCapture.SubscribeDelayed(1.5f);
            MapHandlers.Generating += SeedManager.OnMapGenerating;
            ServerHandlers.RoundStarted += OnRoundStarted;
            ServerHandlers.RoundEnded += OnRoundEnded;
            ServerHandlers.RestartingRound += OnRestartingRound;
            PlayerHandlers.Verified += OnPlayerVerified;
            PlayerHandlers.Left += OnPlayerLeft;

            Log.Info($"{Name} v{Version} enabled.");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            MultiRecorder?.Stop();
            ReplayPlayer?.Stop();
            MultiReplayPlayer?.Stop();
            DummyController?.Clear();

            ItemCapture?.Unsubscribe();
            MapHandlers.Generating -= SeedManager.OnMapGenerating;
            ServerHandlers.RoundStarted -= OnRoundStarted;
            ServerHandlers.RoundEnded -= OnRoundEnded;
            ServerHandlers.RestartingRound -= OnRestartingRound;
            PlayerHandlers.Verified -= OnPlayerVerified;
            PlayerHandlers.Left -= OnPlayerLeft;

            _harmony?.UnpatchAll(_harmony.Id);
            _harmony = null;
            SeedManager = null;
            DummyController = null;
            MultiRecorder = null;
            ReplayPlayer = null;
            MultiReplayPlayer = null;
            ItemCapture = null;
            Instance = null;

            Log.Info($"{Name} disabled.");
            base.OnDisabled();
        }

        public void ScheduleReplay(ReplayBundle bundle, string fileName)
        {
            ScheduledReplay = bundle;
            ScheduledReplayFileName = fileName;
            if (bundle != null)
            {
                SeedManager.QueueSeed(bundle.MapSeed);
                Log.Info($"[ReplaySystem] Scheduled replay '{fileName}' for next round (seed={bundle.MapSeed}, {bundle.Players.Count} players).");
            }
        }

        public void CancelScheduledReplay()
        {
            ScheduledReplay = null;
            ScheduledReplayFileName = null;
            SeedManager.ClearQueue();
            Log.Info("[ReplaySystem] Scheduled replay cancelled.");
        }

        private void OnRoundStarted()
        {
            SeedManager.OnRoundStarted();

            if (ScheduledReplay != null)
            {
                var bundle = ScheduledReplay;
                string fileName = ScheduledReplayFileName;
                ScheduledReplay = null;
                ScheduledReplayFileName = null;

                Log.Info($"[ReplaySystem] Replay round starting — playing '{fileName}' ({bundle.Players.Count} dummies). Players forced to spectator.");
                Timing.CallDelayed(1.5f, () => EnterReplayMode(bundle));
                return;
            }

            MultiRecorder.Start();
            foreach (var p in Player.List)
                MultiRecorder.AddPlayer(p);
            Log.Info($"[ReplaySystem] Auto-record started ({MultiRecorder.Count} players).");
        }

        private void EnterReplayMode(ReplayBundle bundle)
        {
            int spectators = 0;
            foreach (var p in Player.List)
            {
                if (p == null || p.IsNPC) continue;
                try
                {
                    p.Role.Set(RoleTypeId.Spectator);
                    spectators++;
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem] Failed to set spectator for {p.Nickname}: {e.Message}"); }
            }
            Log.Info($"[ReplaySystem] Forced {spectators} player(s) to Spectator.");

            try { Round.IsLocked = true; } catch { }
            Log.Info("[ReplaySystem] Round locked for replay duration.");

            try
            {
                int durationSec = (int)bundle.Duration.TotalSeconds;
                string msg = string.Format(
                    Config.MsgReplayBroadcast,
                    bundle.ServerName ?? "?",
                    bundle.StartedUtc,
                    bundle.Players.Count,
                    durationSec);
                Map.Broadcast(Config.ReplayBroadcastSeconds, msg);
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem] Broadcast failed: {e.Message}"); }

            MultiReplayPlayer.Play(bundle);

            float autoEndDelay = (float)bundle.Duration.TotalSeconds + 5f;
            if (autoEndDelay <= 0f) autoEndDelay = 30f;
            Timing.CallDelayed(autoEndDelay, () =>
            {
                try { Round.IsLocked = false; } catch { }
                Log.Info("[ReplaySystem] Replay finished, round unlocked.");
            });
        }

        private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            StopAndPersist();
        }

        private void OnRestartingRound()
        {
            StopAndPersist();
        }

        private void StopAndPersist()
        {
            if (MultiReplayPlayer != null && MultiReplayPlayer.IsPlaying)
            {
                MultiReplayPlayer.Stop();
                try { Round.IsLocked = false; } catch { }
            }

            if (!MultiRecorder.IsRecording) return;
            int count = MultiRecorder.Count;
            MultiRecorder.Stop();

            if (count == 0)
            {
                Log.Info("[ReplaySystem] Auto-record stopped (no players to persist).");
                return;
            }

            try
            {
                string fileName = ReplayPaths.BuildReplayFileName(
                    MultiRecorder.ServerName,
                    MultiRecorder.StartedUtc,
                    MultiRecorder.EndedUtc,
                    MultiRecorder.Count,
                    MultiRecorder.RoundId);
                string path = Path.Combine(ReplayPaths.ReplayDirectory, fileName);
                using (var fs = File.Create(path))
                    ReplaySerializer.Write(MultiRecorder, fs);

                long size = new FileInfo(path).Length;
                Log.Info($"[ReplaySystem] Replay saved → {fileName} ({count} players, {size / 1024} KB).");

                ReplayStore.Rotate();
            }
            catch (Exception e)
            {
                Log.Error($"[ReplaySystem] Failed to persist replay: {e.GetBaseException().Message}");
            }
        }

        private void OnPlayerVerified(Exiled.Events.EventArgs.Player.VerifiedEventArgs ev)
        {
            if (MultiRecorder.IsRecording)
                MultiRecorder.AddPlayer(ev.Player);
        }

        private void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            if (MultiRecorder.IsRecording)
                MultiRecorder.StopPlayer(ev.Player);
        }
    }
}
