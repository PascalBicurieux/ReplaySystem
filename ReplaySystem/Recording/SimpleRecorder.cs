namespace ReplaySystem.Recording
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.API.Features.Roles;
    using MEC;
    using PlayerRoles;
    using PlayerRoles.FirstPersonControl;
    using ReplaySystem.Models;
    using UnityEngine;

    public class SimpleRecorder
    {
        public bool IsRecording { get; private set; }
        public List<RecordedFrame> Frames { get; } = new List<RecordedFrame>();
        public List<RecordedItemEvent> ItemEvents { get; } = new List<RecordedItemEvent>();
        public List<RecordedExplosion> Explosions { get; } = new List<RecordedExplosion>();
        public List<RecordedInteraction> Interactions { get; } = new List<RecordedInteraction>();
        public List<RecordedDeath> Deaths { get; } = new List<RecordedDeath>();

        public RoleTypeId InitialRole { get; private set; }
        public Vector3 InitialPosition { get; private set; }
        public float InitialYaw { get; private set; }
        public float InitialPitch { get; private set; }
        public string InitialNickname { get; private set; }
        public string InitialUserId { get; private set; }

        public bool HasRecording => !IsRecording && Frames.Count > 0;
        public Player Target => _target;

        private Player _target;
        private CoroutineHandle _handle;
        private float _startTime;
        private string _lastInventorySig;

        public bool Start(Player player)
        {
            if (IsRecording)
            {
                Log.Warn("[ReplaySystem][Rec] Already recording.");
                return false;
            }
            if (player == null)
            {
                Log.Warn("[ReplaySystem][Rec] Target is null.");
                return false;
            }

            Frames.Clear();
            ItemEvents.Clear();
            Explosions.Clear();
            Interactions.Clear();
            Deaths.Clear();
            _target = player;
            _lastInventorySig = null;

            InitialRole = player.Role.Type;
            InitialPosition = player.Position;
            InitialYaw = 0f;
            InitialPitch = 0f;
            if (player.Role is FpcRole fpc)
            {
                var mouseLook = fpc.FirstPersonController.FpcModule.MouseLook;
                InitialYaw = mouseLook.CurrentHorizontal;
                InitialPitch = mouseLook.CurrentVertical;
            }
            InitialNickname = player.Nickname;
            InitialUserId = player.UserId;

            _startTime = Time.time;
            IsRecording = true;
            _handle = Timing.RunCoroutine(RecordLoop());

            Log.Info($"[ReplaySystem][Rec] Started recording '{player.Nickname}' role={InitialRole} pos={InitialPosition}");
            return true;
        }

        public void Stop()
        {
            if (!IsRecording) return;
            IsRecording = false;
            if (_handle.IsRunning) Timing.KillCoroutines(_handle);
            float duration = Frames.Count > 0 ? Frames[Frames.Count - 1].Time : 0f;
            Log.Info($"[ReplaySystem][Rec] Stopped. {Frames.Count} frames + {ItemEvents.Count} item events over {duration:F2}s.");
        }

        public void HydrateInitial(string userId, string nickname, RoleTypeId role, Vector3 position, float yaw, float pitch)
        {
            InitialUserId = userId;
            InitialNickname = nickname;
            InitialRole = role;
            InitialPosition = position;
            InitialYaw = yaw;
            InitialPitch = pitch;
        }

        public void LogItemEvent(Player source, ItemEventKind kind, ItemType type, Vector3 position = default, Vector3 direction = default, byte data = 0)
        {
            if (!IsRecording || source != _target) return;
            float t = Time.time - _startTime;
            ItemEvents.Add(new RecordedItemEvent
            {
                Time = t,
                Kind = kind,
                ItemType = type,
                Position = position,
                Direction = direction,
                Data = data,
            });
            Log.Debug($"[ReplaySystem][Rec] +{t:F2}s {kind} {type}");
        }

        public void LogDeath(byte damageType)
        {
            if (!IsRecording) return;
            float t = Time.time - _startTime;
            Deaths.Add(new RecordedDeath { Time = t, DamageType = damageType });
            Log.Debug($"[ReplaySystem][Rec] +{t:F2}s Death damageType={damageType}");
        }

        public void LogInteraction(InteractionKind kind, bool allowed, uint targetNetId, Vector3 targetPosition, byte data = 0, float dataFloat = 0f)
        {
            if (!IsRecording) return;
            float t = Time.time - _startTime;
            Interactions.Add(new RecordedInteraction
            {
                Time = t,
                Kind = kind,
                Allowed = allowed,
                TargetNetId = targetNetId,
                TargetPosition = targetPosition,
                Data = data,
                DataFloat = dataFloat,
            });
            Log.Debug($"[ReplaySystem][Rec] +{t:F2}s Interaction {kind} netId={targetNetId} allowed={allowed}");
        }

        public void LogExplosion(ItemType projectileType, Vector3 position, byte explosionType)
        {
            if (!IsRecording) return;
            float t = Time.time - _startTime;
            Explosions.Add(new RecordedExplosion
            {
                Time = t,
                ProjectileType = projectileType,
                Position = position,
                ExplosionType = explosionType,
            });
            Log.Debug($"[ReplaySystem][Rec] +{t:F2}s Explosion {projectileType} at {position}");
        }

        private IEnumerator<float> RecordLoop()
        {
            const float dt = 1f / 30f;
            while (IsRecording && _target != null && _target.IsConnected)
            {
                var frame = new RecordedFrame
                {
                    Time = Time.time - _startTime,
                    Position = _target.Position,
                    CurrentItem = _target.CurrentItem?.Type ?? ItemType.None,
                    Health = _target.Health,
                    MaxHealth = _target.MaxHealth,
                    ArtificialHealth = _target.ArtificialHealth,
                    MaxArtificialHealth = _target.MaxArtificialHealth,
                    CurrentRole = (byte)_target.Role.Type,
                    IsCuffed = _target.IsCuffed,
                };

                if (_target.Role is FpcRole fpc)
                {
                    var mouseLook = fpc.FirstPersonController.FpcModule.MouseLook;
                    frame.Yaw = mouseLook.CurrentHorizontal;
                    frame.Pitch = mouseLook.CurrentVertical;
                    frame.MoveState = (byte)fpc.MoveState;
                }

                frame.Inventory = SnapshotInventoryIfChanged();

                try
                {
                    frame.WarheadStatus = (byte)Warhead.Status;
                    frame.WarheadLever = Warhead.LeverStatus;
                    frame.WarheadTimer = Warhead.DetonationTimer;
                }
                catch { }

                CaptureScpState(ref frame);

                Frames.Add(frame);
                yield return Timing.WaitForSeconds(dt);
            }

            if (IsRecording) Stop();
        }

        private void CaptureScpState(ref RecordedFrame frame)
        {
            try
            {
                if (_target.Role is Exiled.API.Features.Roles.Scp096Role s096)
                {
                    frame.Scp096RageState = (byte)s096.RageState;
                    frame.Scp096AbilityState = (byte)s096.AbilityState;
                    frame.Scp096EnragedTimeLeft = s096.EnragedTimeLeft;
                    frame.Scp096TotalEnrageTime = s096.TotalEnrageTime;
                    frame.Scp096EnrageCooldown = s096.EnrageCooldown;
                    frame.Scp096ChargeCooldown = s096.ChargeCooldown;
                    frame.Scp096RemainingCharge = s096.RemainingChargeDuration;
                    frame.Scp096TryNotToCry = s096.TryNotToCryActive;
                }
                else if (_target.Role is Exiled.API.Features.Roles.Scp3114Role s3114)
                {
                    frame.Scp3114DisguiseStatus = (byte)s3114.DisguiseStatus;
                    frame.Scp3114StolenRole = (byte)s3114.StolenRole;
                    frame.Scp3114UnitId = s3114.UnitId;
                    frame.Scp3114DisguiseDuration = s3114.DisguiseDuration;
                    frame.Scp3114WarningTime = s3114.WarningTime;
                }
                else if (_target.Role is Exiled.API.Features.Roles.Scp079Role s079)
                {
                    try { frame.Scp079CameraNetId = s079.Camera != null ? s079.Camera.Id : (ushort)0; } catch { }
                    frame.Scp079Energy = s079.Energy;
                    frame.Scp079Level = (byte)Mathf.Clamp(s079.Level, 0, 255);
                }
            }
            catch { }
        }

        private ItemType[] SnapshotInventoryIfChanged()
        {
            var items = _target.Items;
            int count = 0;
            foreach (var _ in items) count++;
            var buffer = new ItemType[count];
            int idx = 0;
            foreach (var it in items)
                buffer[idx++] = it.Type;
            System.Array.Sort(buffer, (a, b) => ((int)a).CompareTo((int)b));

            string sig = string.Join(",", buffer);
            if (sig == _lastInventorySig)
                return null;
            _lastInventorySig = sig;
            return buffer;
        }
    }
}
