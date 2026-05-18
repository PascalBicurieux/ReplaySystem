namespace ReplaySystem.Models
{
    using UnityEngine;

    public struct RecordedItemEvent
    {
        public float Time;
        public ItemEventKind Kind;
        public ItemType ItemType;
        public Vector3 Position;
        public Vector3 Direction;
        public byte Data;
    }
}
