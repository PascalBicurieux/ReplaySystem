namespace ReplaySystem.Models
{
    public enum InteractionKind : byte
    {
        None = 0,
        DoorInteract = 1,
        LockerOpen = 2,
        ElevatorCall = 3,
        Scp079DoorLock = 10,
        Scp079Tesla = 11,
        Scp079RoomBlackout = 12,
        Scp079ZoneBlackout = 13,
        Scp079Lockdown = 14,
        GeneratorUnlock = 20,
        GeneratorOpen = 21,
        GeneratorClose = 22,
        GeneratorActivate = 23,
        GeneratorStop = 24,
    }
}
