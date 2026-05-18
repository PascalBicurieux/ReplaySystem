namespace ReplaySystem.Models
{
    using UnityEngine;

    public struct RecordedFrame
    {
        public float Time;
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public byte MoveState;

        public float Health;
        public float MaxHealth;
        public float ArtificialHealth;
        public float MaxArtificialHealth;

        public byte CurrentRole;
        public bool IsCuffed;

        public ItemType[] Inventory;
        public ItemType CurrentItem;

        public byte WarheadStatus;
        public bool WarheadLever;
        public float WarheadTimer;

        public byte Scp096RageState;
        public byte Scp096AbilityState;
        public float Scp096EnragedTimeLeft;
        public float Scp096TotalEnrageTime;
        public float Scp096EnrageCooldown;
        public float Scp096ChargeCooldown;
        public float Scp096RemainingCharge;
        public bool Scp096TryNotToCry;

        public byte Scp3114DisguiseStatus;
        public byte Scp3114StolenRole;
        public byte Scp3114UnitId;
        public float Scp3114DisguiseDuration;
        public float Scp3114WarningTime;

        public uint Scp079CameraNetId;
        public float Scp079Energy;
        public byte Scp079Level;
    }
}
