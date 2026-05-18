namespace ReplaySystem.Seed
{
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Map;

    public class SeedManager
    {
        public int? QueuedSeed { get; private set; }
        public int? LastAppliedSeed { get; private set; }

        public void QueueSeed(int seed)
        {
            QueuedSeed = seed;
            Log.Info($"[ReplaySystem] Seed {seed} queued for next round generation.");
        }

        public void ClearQueue()
        {
            QueuedSeed = null;
            Log.Info("[ReplaySystem] Seed queue cleared.");
        }

        public void OnMapGenerating(GeneratingEventArgs ev)
        {
            if (QueuedSeed is int target)
            {
                Log.Info($"[ReplaySystem] Forcing seed {target} (original was {ev.Seed}).");
                ev.Seed = target;
            }
        }

        public void OnRoundStarted()
        {
            int active = Map.Seed;
            LastAppliedSeed = active;
            Log.Debug($"[ReplaySystem] Round started with seed {active}.");

            if (QueuedSeed is int q)
            {
                if (q == active)
                    Log.Debug("[ReplaySystem] Queued seed was applied successfully.");
                else
                    Log.Warn($"[ReplaySystem] Seed mismatch: queued {q} vs active {active}.");

                QueuedSeed = null;
            }
        }
    }
}
