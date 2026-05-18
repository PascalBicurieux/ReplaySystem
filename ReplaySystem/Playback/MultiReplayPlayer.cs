namespace ReplaySystem.Playback
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Features;
    using ReplaySystem.Storage;

    public class MultiReplayPlayer
    {
        private readonly List<ReplayPlayer> _players = new List<ReplayPlayer>();

        public bool IsPlaying => _players.Count > 0;
        public int Count => _players.Count;
        public IReadOnlyList<ReplayPlayer> SubPlayers => _players;

        public bool Play(ReplayBundle bundle)
        {
            Stop();
            if (bundle == null || bundle.Players == null || bundle.Players.Count == 0)
            {
                Log.Warn("[ReplaySystem][MultiPlay] Bundle is empty or null.");
                return false;
            }

            int started = 0;
            foreach (var rec in bundle.Players)
            {
                var rp = new ReplayPlayer();
                if (rp.Play(rec))
                {
                    _players.Add(rp);
                    started++;
                }
                else
                {
                    Log.Warn($"[ReplaySystem][MultiPlay] Failed to start sub-player for {rec.InitialNickname}.");
                }
            }

            Log.Info($"[ReplaySystem][MultiPlay] Playing {started}/{bundle.Players.Count} dummies (round {bundle.RoundId?.Substring(0, 8) ?? "?"}, seed {bundle.MapSeed}).");
            return started > 0;
        }

        public void Stop()
        {
            foreach (var p in _players)
            {
                try { p.Stop(); }
                catch (Exception e) { Log.Warn($"[ReplaySystem][MultiPlay] Sub-player stop failed: {e.Message}"); }
            }
            _players.Clear();
        }
    }
}
