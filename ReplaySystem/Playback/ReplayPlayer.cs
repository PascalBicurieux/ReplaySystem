namespace ReplaySystem.Playback
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Exiled.API.Extensions;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups;
    using Exiled.API.Features.Roles;
    using HarmonyLib;
    using InventorySystem.Items.Firearms.Modules;
    using InventorySystem.Items.Jailbird;
    using InventorySystem.Items.MicroHID.Modules;
    using MEC;
    using PlayerRoles.FirstPersonControl;
    using RelativePositioning;
    using ReplaySystem.Models;
    using ReplaySystem.Recording;
    using UnityEngine;

    public class ReplayPlayer
    {
        private const float SpawnGrace = 0.8f;

        private static readonly PropertyInfo MotorReceivedPositionProp =
            AccessTools.Property(typeof(FpcMotor), "ReceivedPosition");
        private static readonly PropertyInfo MotorVelocityProp =
            AccessTools.Property(typeof(FpcMotor), "Velocity");
        private static readonly MethodInfo AutoServerShootMethod =
            AccessTools.Method(typeof(AutomaticActionModule), "ServerShoot");

        private CoroutineHandle _runHandle;
        private Npc _dummy;
        private ItemType[] _lastDiffedInventory;
        private readonly List<Pickup> _spawnedPickups = new List<Pickup>();

        public bool IsPlaying => _runHandle.IsRunning;
        public Npc Dummy => _dummy;

        public bool Play(SimpleRecorder source)
        {
            if (source == null || source.Frames.Count == 0)
            {
                Log.Warn("[ReplaySystem][Play] No recording to play.");
                return false;
            }

            Stop();

            _dummy = Npc.Spawn(source.InitialNickname, source.InitialRole, ignored: true, source.InitialPosition);
            Plugin.Instance.DummyController.Track(_dummy);
            _lastDiffedInventory = null;

            try { _dummy.IsGodModeEnabled = true; } catch { }

            Log.Info($"[ReplaySystem][Play] Spawned replay dummy '{source.InitialNickname}' role={source.InitialRole}. Playing {source.Frames.Count} frames + {source.ItemEvents.Count} events + {source.Explosions.Count} explosions.");
            _runHandle = Timing.RunCoroutine(RunLoop(source));
            return true;
        }

        public void Stop()
        {
            if (_runHandle.IsRunning) Timing.KillCoroutines(_runHandle);

            foreach (var p in _spawnedPickups)
            {
                if (p == null) continue;
                try { p.Destroy(); } catch { }
            }
            _spawnedPickups.Clear();

            _dummy = null;
            _lastDiffedInventory = null;
        }

        private IEnumerator<float> RunLoop(SimpleRecorder source)
        {
            yield return Timing.WaitForSeconds(SpawnGrace);

            if (_dummy == null || !_dummy.IsConnected)
            {
                Log.Warn("[ReplaySystem][Play] Dummy not ready after spawn grace, aborting.");
                yield break;
            }

            try { _dummy.ClearInventory(); } catch { }

            float t0 = Time.time;
            int frameIdx = 0;
            int eventIdx = 0;
            int explosionIdx = 0;
            int interactionIdx = 0;
            int deathIdx = 0;
            int totalFrames = source.Frames.Count;
            int totalEvents = source.ItemEvents.Count;
            int totalExplosions = source.Explosions.Count;
            int totalInteractions = source.Interactions.Count;
            int totalDeaths = source.Deaths.Count;

            while (frameIdx < totalFrames && _dummy != null && _dummy.IsConnected)
            {
                float elapsed = Time.time - t0;

                while (frameIdx + 1 < totalFrames && source.Frames[frameIdx + 1].Time <= elapsed)
                    frameIdx++;

                var frame = source.Frames[frameIdx];
                ApplyFrame(_dummy, frame);
                SyncInventory(frame);

                while (eventIdx < totalEvents && source.ItemEvents[eventIdx].Time <= elapsed)
                {
                    ApplyItemEvent(source.ItemEvents[eventIdx]);
                    eventIdx++;
                }

                while (explosionIdx < totalExplosions && source.Explosions[explosionIdx].Time <= elapsed)
                {
                    PlayExplosion(source.Explosions[explosionIdx]);
                    explosionIdx++;
                }

                while (interactionIdx < totalInteractions && source.Interactions[interactionIdx].Time <= elapsed)
                {
                    ApplyInteraction(source.Interactions[interactionIdx]);
                    interactionIdx++;
                }

                while (deathIdx < totalDeaths && source.Deaths[deathIdx].Time <= elapsed)
                {
                    ApplyDeath(source.Deaths[deathIdx]);
                    deathIdx++;
                }

                if (frameIdx == totalFrames - 1) break;
                yield return Timing.WaitForOneFrame;
            }

            float endTime = source.Frames[totalFrames - 1].Time;
            while (eventIdx < totalEvents && source.ItemEvents[eventIdx].Time <= endTime + 0.5f)
            {
                ApplyItemEvent(source.ItemEvents[eventIdx]);
                eventIdx++;
            }
            while (explosionIdx < totalExplosions && source.Explosions[explosionIdx].Time <= endTime + 0.5f)
            {
                PlayExplosion(source.Explosions[explosionIdx]);
                explosionIdx++;
            }
            while (interactionIdx < totalInteractions && source.Interactions[interactionIdx].Time <= endTime + 0.5f)
            {
                ApplyInteraction(source.Interactions[interactionIdx]);
                interactionIdx++;
            }
            while (deathIdx < totalDeaths && source.Deaths[deathIdx].Time <= endTime + 0.5f)
            {
                ApplyDeath(source.Deaths[deathIdx]);
                deathIdx++;
            }

            Log.Info($"[ReplaySystem][Play] Playback complete ({frameIdx + 1}/{totalFrames} frames, {eventIdx}/{totalEvents} events, {explosionIdx}/{totalExplosions} explosions, {interactionIdx}/{totalInteractions} interactions, {deathIdx}/{totalDeaths} deaths).");
            if (_dummy?.Role is FpcRole fpc)
            {
                TrySet(MotorVelocityProp, fpc.FirstPersonController.FpcModule.Motor, Vector3.zero);
                fpc.MoveState = PlayerMovementState.Walking;
            }
        }

        private void SyncInventory(RecordedFrame frame)
        {
            if (_dummy == null) return;

            if (frame.Inventory != null && !ReferenceEquals(frame.Inventory, _lastDiffedInventory))
            {
                ApplyInventoryDiff(frame.Inventory);
                _lastDiffedInventory = frame.Inventory;
            }

            ItemType currentType = _dummy.CurrentItem?.Type ?? ItemType.None;
            if (currentType == frame.CurrentItem) return;

            if (frame.CurrentItem == ItemType.None)
            {
                try { _dummy.CurrentItem = null; } catch { }
            }
            else
            {
                var match = _dummy.Items.FirstOrDefault(i => i.Type == frame.CurrentItem);
                if (match != null)
                {
                    try { _dummy.CurrentItem = match; } catch { }
                }
            }
        }

        private void ApplyInventoryDiff(ItemType[] target)
        {
            try
            {
                var targetCount = new Dictionary<ItemType, int>();
                foreach (var t in target)
                {
                    if (t == ItemType.None) continue;
                    targetCount.TryGetValue(t, out int c);
                    targetCount[t] = c + 1;
                }

                var currentCount = new Dictionary<ItemType, int>();
                foreach (var it in _dummy.Items)
                {
                    currentCount.TryGetValue(it.Type, out int c);
                    currentCount[it.Type] = c + 1;
                }

                foreach (var kv in currentCount)
                {
                    int want = targetCount.TryGetValue(kv.Key, out int w) ? w : 0;
                    int extra = kv.Value - want;
                    if (extra <= 0) continue;
                    var toRemove = _dummy.Items.Where(i => i.Type == kv.Key).Take(extra).ToList();
                    foreach (var it in toRemove)
                    {
                        try { _dummy.RemoveItem(it, destroy: true); } catch { }
                    }
                }

                foreach (var kv in targetCount)
                {
                    int have = currentCount.TryGetValue(kv.Key, out int c) ? c : 0;
                    int need = kv.Value - have;
                    for (int i = 0; i < need; i++)
                    {
                        try { _dummy.AddItem(kv.Key); } catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Play] ApplyInventoryDiff failed: {e.Message}");
            }
        }

        private void ApplyItemEvent(RecordedItemEvent ev)
        {
            if (_dummy == null) return;
            try
            {
                switch (ev.Kind)
                {
                    case ItemEventKind.Drop:
                        ApplyDrop(ev);
                        break;
                    case ItemEventKind.Use:
                        if (_dummy.CurrentItem is Usable usable) usable.Use();
                        break;
                    case ItemEventKind.Reload:
                        if (_dummy.CurrentItem is Firearm reloadingFa) reloadingFa.Reload();
                        break;
                    case ItemEventKind.Shoot:
                        ApplyShoot();
                        break;
                    case ItemEventKind.Swinging:
                        ApplySwing();
                        break;
                    case ItemEventKind.MicroHidStateChange:
                        ApplyMicroHidStateChange(ev);
                        break;
                    case ItemEventKind.JailbirdChargeStart:
                        ApplyJailbirdRpc(JailbirdMessageType.ChargeStarted);
                        break;
                    case ItemEventKind.JailbirdChargeComplete:
                        ApplyJailbirdRpc(JailbirdMessageType.ChargeLoadTriggered);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Play] ApplyItemEvent({ev.Kind} {ev.ItemType}) failed: {e.Message}");
            }
        }

        private void ApplyShoot()
        {
            if (_dummy?.CurrentItem is not Firearm firearm) return;
            var baseItem = firearm.Base;
            if (baseItem == null || AutoServerShootMethod == null) return;
            try
            {
                if (baseItem.TryGetModule<AutomaticActionModule>(out var mod))
                {
                    AutoServerShootMethod.Invoke(mod, new object[] { null });
                    Log.Debug($"[ReplaySystem][Play] Shoot {firearm.Type}");
                }
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Shoot failed: {e.GetBaseException().Message}"); }
        }

        private void ApplySwing()
        {
            if (_dummy?.CurrentItem?.Base is JailbirdItem jb)
            {
                try
                {
                    jb.SendRpc(JailbirdMessageType.AttackTriggered, null);
                    Log.Debug($"[ReplaySystem][Play] Jailbird swing");
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Swing failed: {e.Message}"); }
            }
        }

        private void ApplyJailbirdRpc(JailbirdMessageType msg)
        {
            if (_dummy?.CurrentItem?.Base is JailbirdItem jb)
            {
                try
                {
                    jb.SendRpc(msg, null);
                    Log.Debug($"[ReplaySystem][Play] Jailbird RPC {msg}");
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Jailbird RPC {msg} failed: {e.Message}"); }
            }
        }

        private void ApplyMicroHidStateChange(RecordedItemEvent ev)
        {
            if (_dummy?.CurrentItem is MicroHid microHid)
            {
                try
                {
                    microHid.State = (MicroHidPhase)ev.Data;
                    Log.Debug($"[ReplaySystem][Play] MicroHID → {microHid.State}");
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] MicroHID state failed: {e.Message}"); }
            }
        }

        private void ApplyDrop(RecordedItemEvent ev)
        {
            var item = _dummy.Items.FirstOrDefault(i => i.Type == ev.ItemType);
            if (item != null)
            {
                try { _dummy.RemoveItem(item, destroy: true); } catch { }
            }

            Vector3 pos = _dummy.Position;
            Log.Debug($"[ReplaySystem][Play] Drop {ev.ItemType} at {pos}");
            SpawnPickup(ev.ItemType, pos);
        }

        private void PlayExplosion(RecordedExplosion data)
        {
            if (data == null) return;

            ProjectileType projType = data.ProjectileType.GetProjectileType();
            if (projType == ProjectileType.None)
            {
                Log.Warn($"[ReplaySystem][Play] Unmapped projectile type {data.ProjectileType}, skipping explosion");
                return;
            }

            Log.Debug($"[ReplaySystem][Play] Explosion {data.ProjectileType} at {data.Position}");
            try { Map.Explode(data.Position, projType, _dummy); }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Map.Explode failed: {e.GetBaseException().Message}"); }
        }

        private void SpawnPickup(ItemType type, Vector3 position)
        {
            try
            {
                Pickup pickup = Pickup.CreateAndSpawn(type, position, Quaternion.identity);
                if (pickup == null)
                {
                    Log.Warn($"[ReplaySystem][Play] CreateAndSpawn({type}) returned null");
                    return;
                }
                _spawnedPickups.Add(pickup);
                Log.Debug($"[ReplaySystem][Play] Spawned {type} (serial={pickup.Serial}) at {pickup.Position}");
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Play] Spawn {type} failed: {e.Message}");
            }
        }

        private void ApplyDeath(RecordedDeath ev)
        {
            if (_dummy == null || !_dummy.IsConnected) return;
            try
            {
                _dummy.IsGodModeEnabled = false;
                var dt = (Exiled.API.Enums.DamageType)ev.DamageType;
                _dummy.Kill(dt);
                Log.Info($"[ReplaySystem][Play] Dummy killed with damageType={dt}");
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Play] ApplyDeath failed: {e.GetBaseException().Message}");
            }
        }

        private void ApplyInteraction(RecordedInteraction ev)
        {
            if (!ev.Allowed)
            {
                Log.Debug($"[ReplaySystem][Play] Skip denied interaction {ev.Kind} netId={ev.TargetNetId}");
                return;
            }

            try
            {
                switch (ev.Kind)
                {
                    case InteractionKind.DoorInteract:
                        ApplyDoorToggle(ev);
                        break;
                    case InteractionKind.LockerOpen:
                        ApplyLockerOpen(ev);
                        break;
                    case InteractionKind.ElevatorCall:
                        ApplyElevatorCall(ev);
                        break;
                    case InteractionKind.Scp079DoorLock:
                        ApplyScp079DoorLock(ev);
                        break;
                    case InteractionKind.Scp079Tesla:
                        ApplyScp079Tesla(ev);
                        break;
                    case InteractionKind.Scp079RoomBlackout:
                        ApplyScp079RoomBlackout(ev);
                        break;
                    case InteractionKind.Scp079ZoneBlackout:
                        ApplyScp079ZoneBlackout(ev);
                        break;
                    case InteractionKind.Scp079Lockdown:
                        ApplyScp079Lockdown(ev);
                        break;
                    case InteractionKind.GeneratorUnlock:
                    case InteractionKind.GeneratorOpen:
                    case InteractionKind.GeneratorClose:
                    case InteractionKind.GeneratorActivate:
                    case InteractionKind.GeneratorStop:
                        ApplyGenerator(ev);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Play] ApplyInteraction({ev.Kind}) failed: {e.GetBaseException().Message}");
            }
        }

        private static void ApplyDoorToggle(RecordedInteraction ev)
        {
            var door = ResolveDoor(ev.TargetNetId, ev.TargetPosition);
            if (door == null)
            {
                Log.Warn($"[ReplaySystem][Play] Door netId={ev.TargetNetId} not found");
                return;
            }
            door.IsOpen = !door.IsOpen;
            Log.Debug($"[ReplaySystem][Play] Door {door.Type} → IsOpen={door.IsOpen}");
        }

        private static Exiled.API.Features.Doors.Door ResolveDoor(uint netId, Vector3 pos)
        {
            if (netId != 0)
            {
                foreach (var d in Exiled.API.Features.Doors.Door.List)
                {
                    if (d?.Base != null && d.Base.netId == netId) return d;
                }
            }
            Exiled.API.Features.Doors.Door best = null;
            float bestSq = 4f * 4f;
            foreach (var d in Exiled.API.Features.Doors.Door.List)
            {
                if (d == null) continue;
                float sq = (d.Position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }

        private void ApplyLockerOpen(RecordedInteraction ev)
        {
            var locker = ResolveLocker(ev.TargetNetId, ev.TargetPosition);
            if (locker == null)
            {
                Log.Warn($"[ReplaySystem][Play] Locker netId={ev.TargetNetId} not found");
                return;
            }
            int targetIdx = ev.Data;
            int i = 0;
            Exiled.API.Features.Lockers.Chamber target = null;
            foreach (var c in locker.Chambers)
            {
                if (i == targetIdx) { target = c; break; }
                i++;
            }
            if (target == null)
            {
                Log.Warn($"[ReplaySystem][Play] Locker chamberId {targetIdx} out of range");
                return;
            }
            target.IsOpen = !target.IsOpen;
            Log.Debug($"[ReplaySystem][Play] Locker chamber {targetIdx} → IsOpen={target.IsOpen}");
        }

        private static Exiled.API.Features.Lockers.Locker ResolveLocker(uint netId, Vector3 pos)
        {
            Exiled.API.Features.Lockers.Locker best = null;
            float bestSq = 4f * 4f;
            foreach (var l in Exiled.API.Features.Lockers.Locker.List)
            {
                if (l == null) continue;
                if (netId != 0 && l.Base != null && l.Base.netId == netId) return l;
                float sq = (l.Position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = l; }
            }
            return best;
        }

        private void ApplyElevatorCall(RecordedInteraction ev)
        {
            var lift = ResolveLift(ev.TargetNetId, ev.TargetPosition);
            if (lift == null)
            {
                Log.Warn($"[ReplaySystem][Play] Lift netId={ev.TargetNetId} not found");
                return;
            }
            int nextLevel = lift.CurrentLevel == 0 ? 1 : 0;
            lift.TryStart(nextLevel);
            Log.Debug($"[ReplaySystem][Play] Lift {lift.Type} → TryStart(level={nextLevel})");
        }

        private static Lift ResolveLift(uint netId, Vector3 pos)
        {
            if (netId != 0)
            {
                foreach (var l in Lift.List)
                {
                    if (l?.Base != null && l.Base.netId == netId) return l;
                }
            }
            Lift best = null;
            float bestSq = 6f * 6f;
            foreach (var l in Lift.List)
            {
                if (l == null) continue;
                float sq = (l.Position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = l; }
            }
            return best;
        }

        private static void ApplyFrame(Npc npc, RecordedFrame f)
        {
            if (npc == null) return;

            if (f.CurrentRole != 0 && (byte)npc.Role.Type != f.CurrentRole)
            {
                try
                {
                    var newRole = (PlayerRoles.RoleTypeId)f.CurrentRole;
                    npc.Role.Set(newRole);
                    Log.Info($"[ReplaySystem][Play] Dummy role change → {newRole}");
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Role change failed: {e.Message}"); }
            }

            try
            {
                if (npc.IsCuffed != f.IsCuffed)
                {
                    if (f.IsCuffed) npc.Handcuff();
                    else npc.RemoveHandcuffs();
                }
            }
            catch { }

            if (npc.Role is FpcRole fpc)
            {
                var fpcModule = fpc.FirstPersonController.FpcModule;
                var motor = fpcModule.Motor;
                var mouseLook = fpcModule.MouseLook;

                npc.Position = f.Position;
                TrySet(MotorReceivedPositionProp, motor, new RelativePosition(f.Position));
                mouseLook.CurrentHorizontal = f.Yaw;
                mouseLook.CurrentVertical = f.Pitch;
                fpc.MoveState = (PlayerMovementState)f.MoveState;
            }

            if (f.MaxHealth > 0f)
            {
                try { npc.MaxHealth = f.MaxHealth; } catch { }
                try { npc.Health = Mathf.Clamp(f.Health, 0.01f, f.MaxHealth); } catch { }
            }
            if (f.MaxArtificialHealth > 0f)
            {
                try { npc.MaxArtificialHealth = f.MaxArtificialHealth; } catch { }
            }
            try { npc.ArtificialHealth = Mathf.Max(0f, f.ArtificialHealth); } catch { }

            SyncWarheadState(f);
            SyncScpState(npc, f);
        }

        private static void SyncScpState(Npc npc, RecordedFrame f)
        {
            if (npc.Role is Exiled.API.Features.Roles.Scp096Role d096)
            {
                try
                {
                    bool wasEnraged = d096.EnragedTimeLeft > 0.1f;
                    bool shouldBeEnraged = f.Scp096EnragedTimeLeft > 0.1f;
                    if (shouldBeEnraged && !wasEnraged)
                        d096.Enrage(Mathf.Max(0.5f, f.Scp096EnragedTimeLeft));
                    else if (!shouldBeEnraged && wasEnraged)
                        d096.Calm(clearTime: false);

                    d096.EnragedTimeLeft = f.Scp096EnragedTimeLeft;
                    d096.TotalEnrageTime = f.Scp096TotalEnrageTime;
                    d096.EnrageCooldown = f.Scp096EnrageCooldown;
                    d096.ChargeCooldown = f.Scp096ChargeCooldown;
                    d096.RemainingChargeDuration = f.Scp096RemainingCharge;
                    d096.TryNotToCryActive = f.Scp096TryNotToCry;
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp096 sync failed: {e.Message}"); }
            }
            else if (npc.Role is Exiled.API.Features.Roles.Scp3114Role d3114)
            {
                try
                {
                    d3114.DisguiseStatus = (PlayerRoles.PlayableScps.Scp3114.Scp3114Identity.DisguiseStatus)f.Scp3114DisguiseStatus;
                    d3114.StolenRole = (PlayerRoles.RoleTypeId)f.Scp3114StolenRole;
                    d3114.UnitId = f.Scp3114UnitId;
                    d3114.DisguiseDuration = f.Scp3114DisguiseDuration;
                    d3114.WarningTime = f.Scp3114WarningTime;
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp3114 sync failed: {e.Message}"); }
            }
            else if (npc.Role is Exiled.API.Features.Roles.Scp079Role d079)
            {
                try
                {
                    d079.Energy = f.Scp079Energy;
                    d079.Level = f.Scp079Level;
                    if (f.Scp079CameraNetId != 0)
                    {
                        var cam = ResolveCamera(f.Scp079CameraNetId);
                        if (cam != null && (d079.Camera == null || d079.Camera.Id != f.Scp079CameraNetId))
                            d079.Camera = cam;
                    }
                }
                catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079 sync failed: {e.Message}"); }
            }
        }

        private static void ApplyGenerator(RecordedInteraction ev)
        {
            var gen = ResolveGenerator(ev.TargetPosition);
            if (gen == null) { Log.Warn($"[ReplaySystem][Play] Generator not found for {ev.Kind}"); return; }
            try
            {
                switch (ev.Kind)
                {
                    case InteractionKind.GeneratorUnlock:
                        gen.IsUnlocked = true;
                        break;
                    case InteractionKind.GeneratorOpen:
                        gen.IsOpen = true;
                        break;
                    case InteractionKind.GeneratorClose:
                        gen.IsOpen = false;
                        break;
                    case InteractionKind.GeneratorActivate:
                        gen.IsActivating = true;
                        break;
                    case InteractionKind.GeneratorStop:
                        gen.IsActivating = false;
                        break;
                }
                Log.Debug($"[ReplaySystem][Play] Generator {ev.Kind} @ {ev.TargetPosition}");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Generator {ev.Kind} failed: {e.Message}"); }
        }

        private static Exiled.API.Features.Generator ResolveGenerator(Vector3 pos)
        {
            Exiled.API.Features.Generator best = null;
            float bestSq = 5f * 5f;
            foreach (var g in Exiled.API.Features.Generator.List)
            {
                if (g == null) continue;
                try
                {
                    float sq = (g.Position - pos).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = g; }
                }
                catch { }
            }
            return best;
        }

        private static void ApplyScp079DoorLock(RecordedInteraction ev)
        {
            var door = ResolveDoor(ev.TargetNetId, ev.TargetPosition);
            if (door == null) { Log.Warn($"[ReplaySystem][Play] Scp079DoorLock: door not found"); return; }
            try
            {
                if (door.IsLocked) door.Unlock();
                else door.ChangeLock(DoorLockType.Regular079);
                Log.Debug($"[ReplaySystem][Play] Scp079 toggle lock on {door.Type} → IsLocked={door.IsLocked}");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079DoorLock failed: {e.Message}"); }
        }

        private static void ApplyScp079Tesla(RecordedInteraction ev)
        {
            var tesla = ResolveTesla(ev.TargetNetId, ev.TargetPosition);
            if (tesla == null) { Log.Warn($"[ReplaySystem][Play] Scp079Tesla: tesla not found"); return; }
            try
            {
                tesla.Trigger(true);
                Log.Debug($"[ReplaySystem][Play] Scp079 trigger tesla at {tesla.Position}");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079Tesla failed: {e.Message}"); }
        }

        private static void ApplyScp079RoomBlackout(RecordedInteraction ev)
        {
            var room = ResolveRoom(ev.TargetNetId, ev.TargetPosition);
            if (room == null) { Log.Warn($"[ReplaySystem][Play] Scp079RoomBlackout: room not found"); return; }
            float duration = ev.DataFloat > 0f ? ev.DataFloat : 8f;
            try
            {
                room.TurnOffLights(duration);
                Log.Debug($"[ReplaySystem][Play] Scp079 blackout room {room.Type} for {duration:F1}s");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079RoomBlackout failed: {e.Message}"); }
        }

        private static void ApplyScp079ZoneBlackout(RecordedInteraction ev)
        {
            var zone = (Exiled.API.Enums.ZoneType)ev.Data;
            float duration = ev.DataFloat > 0f ? ev.DataFloat : 8f;
            try
            {
                int hit = 0;
                foreach (var r in Exiled.API.Features.Room.List)
                {
                    if (r == null || r.Zone != zone) continue;
                    try { r.TurnOffLights(duration); hit++; } catch { }
                }
                Log.Debug($"[ReplaySystem][Play] Scp079 blackout zone {zone} ({hit} rooms) for {duration:F1}s");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079ZoneBlackout failed: {e.Message}"); }
        }

        private static void ApplyScp079Lockdown(RecordedInteraction ev)
        {
            var room = ResolveRoom(ev.TargetNetId, ev.TargetPosition);
            if (room == null) { Log.Warn($"[ReplaySystem][Play] Scp079Lockdown: room not found"); return; }
            try
            {
                foreach (var door in room.Doors)
                {
                    try { door?.Lock(15f, DoorLockType.Lockdown079); } catch { }
                }
                Log.Debug($"[ReplaySystem][Play] Scp079 lockdown room {room.Type}");
            }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Play] Scp079Lockdown failed: {e.Message}"); }
        }

        private static Exiled.API.Features.TeslaGate ResolveTesla(uint netId, Vector3 pos)
        {
            Exiled.API.Features.TeslaGate best = null;
            float bestSq = 5f * 5f;
            foreach (var t in Exiled.API.Features.TeslaGate.List)
            {
                if (t == null) continue;
                try { if (netId != 0 && t.Base != null && t.Base.netId == netId) return t; } catch { }
                try
                {
                    float sq = (t.Position - pos).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = t; }
                }
                catch { }
            }
            return best;
        }

        private static Exiled.API.Features.Room ResolveRoom(uint netId, Vector3 pos)
        {
            Exiled.API.Features.Room best = null;
            float bestSq = 12f * 12f;
            foreach (var r in Exiled.API.Features.Room.List)
            {
                if (r == null) continue;
                try
                {
                    float sq = (r.Position - pos).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = r; }
                }
                catch { }
            }
            return best;
        }

        private static Exiled.API.Features.Camera ResolveCamera(uint id)
        {
            foreach (var c in Exiled.API.Features.Camera.List)
            {
                try { if (c != null && c.Id == id) return c; } catch { }
            }
            return null;
        }

        private static int _lastWarheadSyncFrame = -1;

        private static void SyncWarheadState(RecordedFrame f)
        {
            int frame = Time.frameCount;
            if (frame == _lastWarheadSyncFrame) return;
            _lastWarheadSyncFrame = frame;

            try
            {
                Warhead.LeverStatus = f.WarheadLever;

                var target = (Exiled.API.Enums.WarheadStatus)f.WarheadStatus;
                var current = Warhead.Status;

                if (current == target)
                {
                    if (target == Exiled.API.Enums.WarheadStatus.InProgress)
                        Warhead.DetonationTimer = f.WarheadTimer;
                    return;
                }

                switch (target)
                {
                    case Exiled.API.Enums.WarheadStatus.InProgress:
                        Warhead.Start();
                        break;
                    case Exiled.API.Enums.WarheadStatus.NotArmed:
                        if (current == Exiled.API.Enums.WarheadStatus.InProgress)
                            Warhead.Stop();
                        break;
                    case Exiled.API.Enums.WarheadStatus.Detonated:
                        if (!Warhead.IsDetonated)
                            Warhead.Detonate();
                        break;
                }
            }
            catch { }
        }

        private static void TrySet(PropertyInfo prop, object target, object value)
        {
            try { prop?.SetValue(target, value); }
            catch { }
        }
    }
}
