namespace ReplaySystem.Models
{
    public enum ItemEventKind : byte
    {
        None = 0,
        Equip = 1,
        Unequip = 2,
        Drop = 3,
        Pickup = 4,
        Use = 5,
        Reload = 6,
        Unload = 7,
        Inspect = 8,
        Shoot = 9,
        AimStart = 10,
        AimStop = 11,
        ToggleFlashlight = 12,
        DryFire = 13,
        ThrowRequest = 14,
        ThrownProjectile = 15,
        SearchingPickup = 16,
        UsingStart = 17,
        UsingCancel = 18,
        ChangingAmmo = 19,
        Swinging = 20,
        MicroHidStateChange = 21,
        JailbirdChargeStart = 22,
        JailbirdChargeComplete = 23,
    }
}
