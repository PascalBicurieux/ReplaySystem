namespace ReplaySystem.Recording
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Player;
    using ItemArgs = Exiled.Events.EventArgs.Item;
    using MapArgs = Exiled.Events.EventArgs.Map;
    using MEC;
    using ReplaySystem.Models;
    using PlayerHandlers = Exiled.Events.Handlers.Player;
    using ItemHandlers = Exiled.Events.Handlers.Item;
    using MapHandlers = Exiled.Events.Handlers.Map;
    using Scp079Handlers = Exiled.Events.Handlers.Scp079;
    using Scp079Args = Exiled.Events.EventArgs.Scp079;

    public class ItemEventCapture
    {
        private readonly MultiRecorder _multi;

        public ItemEventCapture(MultiRecorder multi)
        {
            _multi = multi;
        }

        public void SubscribeDelayed(float delaySeconds = 1.5f)
        {
            Timing.CallDelayed(delaySeconds, Subscribe);
        }

        public void Subscribe()
        {
            SubscribeViaReflection(typeof(PlayerHandlers), "ChangedItem", new Action<ChangedItemEventArgs>(OnChangedItem));
            SubscribeViaReflection(typeof(PlayerHandlers), "DroppedItem", new Action<DroppedItemEventArgs>(OnDroppedItem));
            SubscribeViaReflection(typeof(PlayerHandlers), "PickingUpItem", new Action<PickingUpItemEventArgs>(OnPickingUpItem));
            SubscribeViaReflection(typeof(PlayerHandlers), "SearchingPickup", new Action<SearchingPickupEventArgs>(OnSearchingPickup));
            SubscribeViaReflection(typeof(PlayerHandlers), "UsedItem", new Action<UsedItemEventArgs>(OnUsedItem));
            SubscribeViaReflection(typeof(PlayerHandlers), "UsingItem", new Action<UsingItemEventArgs>(OnUsingItem));
            SubscribeViaReflection(typeof(PlayerHandlers), "CancellingItemUse", new Action<CancellingItemUseEventArgs>(OnCancellingItemUse));
            SubscribeViaReflection(typeof(PlayerHandlers), "ReloadedWeapon", new Action<ReloadedWeaponEventArgs>(OnReloadedWeapon));
            SubscribeViaReflection(typeof(PlayerHandlers), "UnloadedWeapon", new Action<UnloadedWeaponEventArgs>(OnUnloadedWeapon));
            SubscribeViaReflection(typeof(PlayerHandlers), "DryfiringWeapon", new Action<DryfiringWeaponEventArgs>(OnDryFire));
            SubscribeViaReflection(typeof(PlayerHandlers), "Shot", new Action<ShotEventArgs>(OnShot));
            SubscribeExplodingGrenade();
            SubscribeViaReflection(typeof(PlayerHandlers), "AimingDownSight", new Action<AimingDownSightEventArgs>(OnAimingDownSight));
            SubscribeViaReflection(typeof(PlayerHandlers), "TogglingWeaponFlashlight", new Action<TogglingWeaponFlashlightEventArgs>(OnTogglingFlashlight));
            SubscribeViaReflection(typeof(PlayerHandlers), "ChangingMicroHIDState", new Action<ChangingMicroHIDStateEventArgs>(OnChangingMicroHIDState));
            SubscribeViaReflection(typeof(ItemHandlers), "ChargingJailbird", new Action<ItemArgs.ChargingJailbirdEventArgs>(OnChargingJailbird));
            SubscribeViaReflection(typeof(ItemHandlers), "JailbirdChargeComplete", new Action<ItemArgs.JailbirdChargeCompleteEventArgs>(OnJailbirdChargeComplete));
            SubscribeViaReflection(typeof(ItemHandlers), "InspectingItem", new Action<ItemArgs.InspectingItemEventArgs>(OnInspectingItem));
            SubscribeViaReflection(typeof(ItemHandlers), "Swinging", new Action<ItemArgs.SwingingEventArgs>(OnSwinging));

            SubscribeInteractions();
        }

        public void Unsubscribe()
        {
            UnsubscribeViaReflection(typeof(PlayerHandlers), "ChangedItem", new Action<ChangedItemEventArgs>(OnChangedItem));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "DroppedItem", new Action<DroppedItemEventArgs>(OnDroppedItem));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "PickingUpItem", new Action<PickingUpItemEventArgs>(OnPickingUpItem));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "SearchingPickup", new Action<SearchingPickupEventArgs>(OnSearchingPickup));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "UsedItem", new Action<UsedItemEventArgs>(OnUsedItem));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "UsingItem", new Action<UsingItemEventArgs>(OnUsingItem));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "CancellingItemUse", new Action<CancellingItemUseEventArgs>(OnCancellingItemUse));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "ReloadedWeapon", new Action<ReloadedWeaponEventArgs>(OnReloadedWeapon));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "UnloadedWeapon", new Action<UnloadedWeaponEventArgs>(OnUnloadedWeapon));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "DryfiringWeapon", new Action<DryfiringWeaponEventArgs>(OnDryFire));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "Shot", new Action<ShotEventArgs>(OnShot));
            UnsubscribeExplodingGrenade();
            UnsubscribeViaReflection(typeof(PlayerHandlers), "AimingDownSight", new Action<AimingDownSightEventArgs>(OnAimingDownSight));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "TogglingWeaponFlashlight", new Action<TogglingWeaponFlashlightEventArgs>(OnTogglingFlashlight));
            UnsubscribeViaReflection(typeof(PlayerHandlers), "ChangingMicroHIDState", new Action<ChangingMicroHIDStateEventArgs>(OnChangingMicroHIDState));
            UnsubscribeViaReflection(typeof(ItemHandlers), "ChargingJailbird", new Action<ItemArgs.ChargingJailbirdEventArgs>(OnChargingJailbird));
            UnsubscribeViaReflection(typeof(ItemHandlers), "JailbirdChargeComplete", new Action<ItemArgs.JailbirdChargeCompleteEventArgs>(OnJailbirdChargeComplete));
            UnsubscribeViaReflection(typeof(ItemHandlers), "InspectingItem", new Action<ItemArgs.InspectingItemEventArgs>(OnInspectingItem));
            UnsubscribeViaReflection(typeof(ItemHandlers), "Swinging", new Action<ItemArgs.SwingingEventArgs>(OnSwinging));

            UnsubscribeInteractions();
        }

        private void SubscribeInteractions()
        {
            TrySubscribeDirect(() => { PlayerHandlers.InteractingDoor += OnInteractingDoor; }, "InteractingDoor");
            TrySubscribeDirect(() => { PlayerHandlers.InteractingLocker += OnInteractingLocker; }, "InteractingLocker");
            TrySubscribeDirect(() => { PlayerHandlers.InteractingElevator += OnInteractingElevator; }, "InteractingElevator");
            TrySubscribeDirect(() => { PlayerHandlers.Dying += OnDying; }, "Dying");
            TrySubscribeDirect(() => { Scp079Handlers.TriggeringDoor += OnScp079TriggeringDoor; }, "Scp079.TriggeringDoor");
            TrySubscribeDirect(() => { Scp079Handlers.InteractingTesla += OnScp079Tesla; }, "Scp079.InteractingTesla");
            TrySubscribeDirect(() => { Scp079Handlers.RoomBlackout += OnScp079RoomBlackout; }, "Scp079.RoomBlackout");
            TrySubscribeDirect(() => { Scp079Handlers.ZoneBlackout += OnScp079ZoneBlackout; }, "Scp079.ZoneBlackout");
            TrySubscribeDirect(() => { Scp079Handlers.LockingDown += OnScp079Lockdown; }, "Scp079.LockingDown");
            TrySubscribeDirect(() => { PlayerHandlers.UnlockingGenerator += OnUnlockingGenerator; }, "UnlockingGenerator");
            TrySubscribeDirect(() => { PlayerHandlers.OpeningGenerator += OnOpeningGenerator; }, "OpeningGenerator");
            TrySubscribeDirect(() => { PlayerHandlers.ClosingGenerator += OnClosingGenerator; }, "ClosingGenerator");
            TrySubscribeDirect(() => { PlayerHandlers.ActivatingGenerator += OnActivatingGenerator; }, "ActivatingGenerator");
            TrySubscribeDirect(() => { PlayerHandlers.StoppingGenerator += OnStoppingGenerator; }, "StoppingGenerator");
        }

        private void UnsubscribeInteractions()
        {
            try { PlayerHandlers.InteractingDoor -= OnInteractingDoor; } catch { }
            try { PlayerHandlers.InteractingLocker -= OnInteractingLocker; } catch { }
            try { PlayerHandlers.InteractingElevator -= OnInteractingElevator; } catch { }
            try { PlayerHandlers.Dying -= OnDying; } catch { }
            try { Scp079Handlers.TriggeringDoor -= OnScp079TriggeringDoor; } catch { }
            try { Scp079Handlers.InteractingTesla -= OnScp079Tesla; } catch { }
            try { Scp079Handlers.RoomBlackout -= OnScp079RoomBlackout; } catch { }
            try { Scp079Handlers.ZoneBlackout -= OnScp079ZoneBlackout; } catch { }
            try { Scp079Handlers.LockingDown -= OnScp079Lockdown; } catch { }
            try { PlayerHandlers.UnlockingGenerator -= OnUnlockingGenerator; } catch { }
            try { PlayerHandlers.OpeningGenerator -= OnOpeningGenerator; } catch { }
            try { PlayerHandlers.ClosingGenerator -= OnClosingGenerator; } catch { }
            try { PlayerHandlers.ActivatingGenerator -= OnActivatingGenerator; } catch { }
            try { PlayerHandlers.StoppingGenerator -= OnStoppingGenerator; } catch { }
        }

        private static void TrySubscribeDirect(Action subscribe, string name)
        {
            try
            {
                subscribe();
                Log.Debug($"[ReplaySystem][Capture] {name}: direct subscribe OK");
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Capture] {name} direct subscribe failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private static void SubscribeViaReflection(Type handlerClass, string eventName, Delegate userHandler)
        {
            try
            {
                var prop = handlerClass.GetProperty(eventName, BindingFlags.Public | BindingFlags.Static);
                if (prop == null) { Log.Warn($"[ReplaySystem][Capture] {eventName}: property not found"); return; }
                object inst = prop.GetValue(null);
                if (inst == null) { Log.Warn($"[ReplaySystem][Capture] {eventName}: instance null"); return; }

                var patchedField = FindFieldByName(inst.GetType(), "<Patched>k__BackingField")
                    ?? FindFieldByName(inst.GetType(), "Patched");
                if (patchedField != null)
                {
                    try
                    {
                        bool wasPatched = (bool)patchedField.GetValue(inst);
                        if (!wasPatched) patchedField.SetValue(inst, true);
                    }
                    catch (Exception pe)
                    {
                        Log.Warn($"[ReplaySystem][Capture] {eventName}: could not read/set Patched: {pe.Message}");
                    }
                }

                Delegate handlerToInvoke = TryConvertToCustomEventHandler(inst, userHandler);
                if (handlerToInvoke == null)
                {
                    Log.Warn($"[ReplaySystem][Capture] {eventName}: could not build CustomEventHandler delegate");
                    return;
                }

                var subscribeMethods = inst.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.Name == "Subscribe")
                    .ToList();

                var twoParam = subscribeMethods.FirstOrDefault(m => m.GetParameters().Length == 2);
                if (twoParam != null)
                {
                    twoParam.Invoke(inst, new object[] { handlerToInvoke, 0 });
                    Log.Debug($"[ReplaySystem][Capture] {eventName}: subscribed (2-param)");
                    return;
                }

                var oneParam = subscribeMethods.FirstOrDefault(m => m.GetParameters().Length == 1);
                if (oneParam != null)
                {
                    oneParam.Invoke(inst, new object[] { handlerToInvoke });
                    Log.Debug($"[ReplaySystem][Capture] {eventName}: subscribed (1-param)");
                    return;
                }

                Log.Warn($"[ReplaySystem][Capture] {eventName}: no Subscribe method found");
            }
            catch (Exception e)
            {
                var inner = e.InnerException ?? e;
                Log.Warn($"[ReplaySystem][Capture] {eventName} reflection subscribe failed: {inner.GetType().Name}: {inner.Message}");
            }
        }

        private static FieldInfo FindFieldByName(Type t, string name)
        {
            while (t != null && t != typeof(object))
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        private static void UnsubscribeViaReflection(Type handlerClass, string eventName, Delegate userHandler)
        {
            try
            {
                var prop = handlerClass.GetProperty(eventName, BindingFlags.Public | BindingFlags.Static);
                object inst = prop?.GetValue(null);
                if (inst == null) return;

                Delegate handlerToInvoke = TryConvertToCustomEventHandler(inst, userHandler);
                if (handlerToInvoke == null) return;

                var unsubscribe = inst.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "Unsubscribe");
                if (unsubscribe != null)
                    unsubscribe.Invoke(inst, new object[] { handlerToInvoke });
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Capture] Unsubscribe {eventName} failed: {e.Message}");
            }
        }

        private static Delegate TryConvertToCustomEventHandler(object eventInstance, Delegate userHandler)
        {
            try
            {
                Type eventType = eventInstance.GetType();
                if (!eventType.IsGenericType) return userHandler;
                Type eventArgsType = eventType.GetGenericArguments()[0];

                Type customHandlerType =
                    Type.GetType($"Exiled.Events.Features.CustomEventHandler`1, Exiled.Events", throwOnError: false)
                    ?? eventInstance.GetType().Assembly.GetType("Exiled.Events.Features.CustomEventHandler`1");
                if (customHandlerType == null) return userHandler;

                Type closedHandler = customHandlerType.MakeGenericType(eventArgsType);
                MethodInfo target = userHandler.Method;
                return Delegate.CreateDelegate(closedHandler, userHandler.Target, target);
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Capture] CustomEventHandler conversion failed: {e.Message}");
                return null;
            }
        }

        private void OnChangedItem(ChangedItemEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            if (ev.Item == null)
                rec.LogItemEvent(ev.Player, ItemEventKind.Unequip, ItemType.None);
            else
                rec.LogItemEvent(ev.Player, ItemEventKind.Equip, ev.Item.Type);
        }

        private void OnDroppedItem(DroppedItemEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            UnityEngine.Vector3 pos = ev.Pickup?.Position ?? ev.Player?.Position ?? UnityEngine.Vector3.zero;
            rec.LogItemEvent(ev.Player, ItemEventKind.Drop, ev.Pickup?.Type ?? ItemType.None, pos);
            Log.Debug($"[ReplaySystem][Rec] Drop captured: {ev.Pickup?.Type} at {pos}");
        }

        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev == null || !ev.IsAllowed) return;
            var rec = _multi.GetRecorder(ev.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Pickup, ev.Pickup?.Type ?? ItemType.None);
        }

        private void OnUsedItem(UsedItemEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Use, ev.Item?.Type ?? ItemType.None);
        }

        private void OnReloadedWeapon(ReloadedWeaponEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Reload, ev.Firearm?.Type ?? ItemType.None);
        }

        private void OnUnloadedWeapon(UnloadedWeaponEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Unload, ev.Firearm?.Type ?? ItemType.None);
        }

        private void OnDryFire(DryfiringWeaponEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.DryFire, ev.Player?.CurrentItem?.Type ?? ItemType.None);
        }

        private void OnShot(ShotEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Shoot, ev.Player?.CurrentItem?.Type ?? ItemType.None);
        }

        private void OnInspectingItem(ItemArgs.InspectingItemEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Inspect, ev.Item?.Type ?? ItemType.None);
        }

        private void OnUsingItem(UsingItemEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.UsingStart, ev.Item?.Type ?? ItemType.None);
        }

        private void OnCancellingItemUse(CancellingItemUseEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.UsingCancel, ev.Item?.Type ?? ItemType.None);
        }

        private void OnSearchingPickup(SearchingPickupEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.SearchingPickup, ev.Pickup?.Type ?? ItemType.None);
        }

        private void SubscribeExplodingGrenade()
        {
            try
            {
                MapHandlers.ExplodingGrenade += OnExplodingGrenade;
                Log.Debug("[ReplaySystem][Capture] ExplodingGrenade: direct subscribe OK");
            }
            catch (Exception e)
            {
                Log.Warn($"[ReplaySystem][Capture] ExplodingGrenade direct subscribe failed: {e.GetType().Name}: {e.Message}");
            }
        }

        private void UnsubscribeExplodingGrenade()
        {
            try { MapHandlers.ExplodingGrenade -= OnExplodingGrenade; }
            catch (Exception e) { Log.Warn($"[ReplaySystem][Capture] ExplodingGrenade unsubscribe failed: {e.Message}"); }
        }

        private void OnExplodingGrenade(MapArgs.ExplodingGrenadeEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            ItemType projectileType = ev.Projectile != null ? ev.Projectile.Type : ItemType.None;
            byte explosionType = (byte)ev.ExplosionType;
            rec.LogExplosion(projectileType, ev.Position, explosionType);
        }

        private void OnAimingDownSight(AimingDownSightEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            ItemType t = ev.Firearm?.Type ?? ev.Player?.CurrentItem?.Type ?? ItemType.None;
            bool isAiming = TryGetBoolProperty(ev, "Adsing") ?? TryGetBoolProperty(ev, "IsAimingDownSight") ?? false;
            rec.LogItemEvent(ev.Player, isAiming ? ItemEventKind.AimStart : ItemEventKind.AimStop, t);
        }

        private void OnTogglingFlashlight(TogglingWeaponFlashlightEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.ToggleFlashlight, ev.Firearm?.Type ?? ItemType.None);
        }

        private void OnSwinging(ItemArgs.SwingingEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.Swinging, ev.Item?.Type ?? ItemType.None);
        }

        private void OnChangingMicroHIDState(ChangingMicroHIDStateEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            byte phase = (byte)ev.NewPhase;
            rec.LogItemEvent(ev.Player, ItemEventKind.MicroHidStateChange, ItemType.MicroHID, default, default, phase);
        }

        private void OnChargingJailbird(ItemArgs.ChargingJailbirdEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.JailbirdChargeStart, ItemType.Jailbird);
        }

        private void OnJailbirdChargeComplete(ItemArgs.JailbirdChargeCompleteEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            rec.LogItemEvent(ev.Player, ItemEventKind.JailbirdChargeComplete, ItemType.Jailbird);
        }

        private void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Door?.Base == null) return;
            rec.LogInteraction(InteractionKind.DoorInteract, ev.IsAllowed, ev.Door.Base.netId, ev.Door.Position);
        }

        private void OnInteractingLocker(InteractingLockerEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.InteractingLocker == null) return;
            uint netId = 0;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { netId = ev.InteractingLocker.Base != null ? ev.InteractingLocker.Base.netId : 0u; } catch { }
            try { pos = ev.InteractingLocker.Position; } catch { }

            byte chamberId = 0;
            if (ev.InteractingChamber != null && ev.InteractingLocker.Chambers != null)
            {
                int idx = 0;
                foreach (var c in ev.InteractingLocker.Chambers)
                {
                    if (c == ev.InteractingChamber) { chamberId = (byte)idx; break; }
                    idx++;
                }
            }
            rec.LogInteraction(InteractionKind.LockerOpen, ev.IsAllowed, netId, pos, chamberId);
        }

        private void OnInteractingElevator(InteractingElevatorEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Lift == null) return;
            uint netId = 0;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { netId = ev.Lift.Base != null ? ev.Lift.Base.netId : 0u; } catch { }
            try { pos = ev.Lift.Position; } catch { }
            rec.LogInteraction(InteractionKind.ElevatorCall, ev.IsAllowed, netId, pos);
        }

        private void OnDying(DyingEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            byte dt = 0;
            try { dt = (byte)ev.DamageHandler.Type; } catch { }
            rec.LogDeath(dt);
        }

        private void OnScp079TriggeringDoor(Scp079Args.TriggeringDoorEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Door?.Base == null) return;
            rec.LogInteraction(InteractionKind.Scp079DoorLock, ev.IsAllowed, ev.Door.Base.netId, ev.Door.Position);
        }

        private void OnScp079Tesla(Scp079Args.InteractingTeslaEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Tesla == null) return;
            uint netId = 0;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { netId = ev.Tesla.Base != null ? ev.Tesla.Base.netId : 0u; } catch { }
            try { pos = ev.Tesla.Position; } catch { }
            rec.LogInteraction(InteractionKind.Scp079Tesla, ev.IsAllowed, netId, pos);
        }

        private void OnScp079RoomBlackout(Scp079Args.RoomBlackoutEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Room == null) return;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { pos = ev.Room.Position; } catch { }
            rec.LogInteraction(InteractionKind.Scp079RoomBlackout, ev.IsAllowed, 0u, pos, 0, ev.BlackoutDuration);
        }

        private void OnScp079ZoneBlackout(Scp079Args.ZoneBlackoutEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null) return;
            byte zone = (byte)ev.Zone;
            rec.LogInteraction(InteractionKind.Scp079ZoneBlackout, ev.IsAllowed, 0u, UnityEngine.Vector3.zero, zone, ev.BlackoutDuration);
        }

        private void OnScp079Lockdown(Scp079Args.LockingDownEventArgs ev)
        {
            var rec = _multi.GetRecorder(ev?.Player);
            if (rec == null || ev.Room == null) return;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { pos = ev.Room.Position; } catch { }
            rec.LogInteraction(InteractionKind.Scp079Lockdown, ev.IsAllowed, 0u, pos);
        }

        private void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
            => LogGenerator(InteractionKind.GeneratorUnlock, ev?.Player, ev?.Generator, ev?.IsAllowed ?? false);
        private void OnOpeningGenerator(OpeningGeneratorEventArgs ev)
            => LogGenerator(InteractionKind.GeneratorOpen, ev?.Player, ev?.Generator, ev?.IsAllowed ?? false);
        private void OnClosingGenerator(ClosingGeneratorEventArgs ev)
            => LogGenerator(InteractionKind.GeneratorClose, ev?.Player, ev?.Generator, ev?.IsAllowed ?? false);
        private void OnActivatingGenerator(ActivatingGeneratorEventArgs ev)
            => LogGenerator(InteractionKind.GeneratorActivate, ev?.Player, ev?.Generator, ev?.IsAllowed ?? false);
        private void OnStoppingGenerator(StoppingGeneratorEventArgs ev)
            => LogGenerator(InteractionKind.GeneratorStop, ev?.Player, ev?.Generator, ev?.IsAllowed ?? false);

        private void LogGenerator(InteractionKind kind, Player player, Exiled.API.Features.Generator gen, bool allowed)
        {
            var rec = _multi.GetRecorder(player);
            if (rec == null || gen == null) return;
            UnityEngine.Vector3 pos = UnityEngine.Vector3.zero;
            try { pos = gen.Position; } catch { }
            rec.LogInteraction(kind, allowed, 0u, pos);
        }

        private static bool? TryGetBoolProperty(object obj, string name)
        {
            var prop = obj.GetType().GetProperty(name);
            if (prop == null || prop.PropertyType != typeof(bool)) return null;
            try { return (bool)prop.GetValue(obj); }
            catch { return null; }
        }
    }
}
