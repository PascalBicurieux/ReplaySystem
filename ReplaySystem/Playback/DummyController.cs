namespace ReplaySystem.Playback
{
    using System.Collections.Generic;
    using Exiled.API.Features;

    public class DummyController
    {
        private readonly List<Npc> _dummies = new List<Npc>();

        public IReadOnlyList<Npc> Dummies => _dummies;

        public void Track(Npc npc)
        {
            if (npc != null && !_dummies.Contains(npc))
                _dummies.Add(npc);
        }

        public int Clear()
        {
            int count = _dummies.Count;
            foreach (Npc npc in _dummies)
            {
                if (npc != null && npc.IsConnected)
                {
                    try { npc.Destroy(); } catch { }
                }
            }
            _dummies.Clear();
            return count;
        }
    }
}
