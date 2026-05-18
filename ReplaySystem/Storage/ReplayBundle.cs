namespace ReplaySystem.Storage
{
    using System;
    using System.Collections.Generic;
    using ReplaySystem.Recording;

    public class ReplayBundle
    {
        public const byte CurrentVersion = 2;

        public byte Version;
        public string ServerName;
        public string RoundId;
        public DateTime StartedUtc;
        public DateTime EndedUtc;
        public int MapSeed;
        public List<SimpleRecorder> Players = new List<SimpleRecorder>();

        public TimeSpan Duration => EndedUtc - StartedUtc;
    }
}
