namespace ReplaySystem.Models
{
    using UnityEngine;

    public struct RecordedInteraction
    {
        public float Time;
        public InteractionKind Kind;
        public bool Allowed;
        public uint TargetNetId;
        public Vector3 TargetPosition;
        public byte Data;
        public float DataFloat;
    }
}
