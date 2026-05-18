namespace ReplaySystem.Recording
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Features;

    public class MultiRecorder
    {
        private readonly Dictionary<string, SimpleRecorder> _byUserId = new Dictionary<string, SimpleRecorder>();

        public bool IsRecording { get; private set; }
        public IReadOnlyDictionary<string, SimpleRecorder> Recorders => _byUserId;
        public int Count => _byUserId.Count;

        public string ServerName { get; private set; }
        public string RoundId { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public DateTime EndedUtc { get; private set; }
        public int MapSeed { get; private set; }

        public void Start()
        {
            if (IsRecording)
            {
                Log.Warn("[ReplaySystem][Multi] Already recording.");
                return;
            }
            _byUserId.Clear();
            RoundId = Guid.NewGuid().ToString("N");
            StartedUtc = DateTime.UtcNow;
            EndedUtc = default;
            try { MapSeed = Map.Seed; } catch { MapSeed = 0; }
            try { ServerName = Server.Name; } catch { ServerName = "Server"; }
            IsRecording = true;
            Log.Info($"[ReplaySystem][Multi] Started. RoundId={RoundId.Substring(0, 8)} Seed={MapSeed}");
        }

        public bool AddPlayer(Player p)
        {
            if (!IsRecording || p == null || p.IsNPC || string.IsNullOrEmpty(p.UserId)) return false;
            if (_byUserId.ContainsKey(p.UserId)) return false;
            var rec = new SimpleRecorder();
            if (!rec.Start(p))
            {
                Log.Warn($"[ReplaySystem][Multi] Failed to start recorder for {p.Nickname}");
                return false;
            }
            _byUserId[p.UserId] = rec;
            Log.Debug($"[ReplaySystem][Multi] Added {p.Nickname} ({_byUserId.Count} players tracked).");
            return true;
        }

        public void StopPlayer(Player p)
        {
            if (p?.UserId == null) return;
            if (_byUserId.TryGetValue(p.UserId, out var rec) && rec.IsRecording)
                rec.Stop();
        }

        public void Stop()
        {
            if (!IsRecording) return;
            IsRecording = false;
            EndedUtc = DateTime.UtcNow;
            foreach (var rec in _byUserId.Values)
                if (rec.IsRecording) rec.Stop();
            Log.Info($"[ReplaySystem][Multi] Stopped. {_byUserId.Count} players tracked.");
        }

        public void Clear()
        {
            Stop();
            _byUserId.Clear();
        }

        public SimpleRecorder GetRecorder(Player p)
        {
            if (p?.UserId == null) return null;
            return _byUserId.TryGetValue(p.UserId, out var rec) ? rec : null;
        }
    }
}
