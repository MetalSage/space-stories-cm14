using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Nuke;

[Serializable, NetSerializable]
public enum STNukeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class STNukeBuiState : BoundUserInterfaceState
{
    public bool Anchor;
    public bool Safety;
    public bool Timing;
    public string TimeLeft;
    public bool CommandLockout;
    public bool Allowed;
    public bool DecryptionComplete;
    public bool Decrypting;
    public string DecryptionTime;
    public bool CanDisengage;

    public STNukeBuiState(
        bool anchor,
        bool safety,
        bool timing,
        string timeLeft,
        bool commandLockout,
        bool allowed,
        bool decryptionComplete,
        bool decrypting,
        string decryptionTime,
        bool canDisengage)
    {
        Anchor = anchor;
        Safety = safety;
        Timing = timing;
        TimeLeft = timeLeft;
        CommandLockout = commandLockout;
        Allowed = allowed;
        DecryptionComplete = decryptionComplete;
        Decrypting = decrypting;
        DecryptionTime = decryptionTime;
        CanDisengage = canDisengage;
    }
}

[Serializable, NetSerializable]
public sealed class STNukeToggleMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class STNukeToggleSafetyMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class STNukeToggleCommandLockoutMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class STNukeToggleAnchorMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class STNukeToggleEncryptionMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class STNukeDefuseDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class STNukeAnchorDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class STNukeSafetyDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class STNukeEncryptionDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class STNukeXenoResinDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum STNukeVisuals : byte
{
    Deployed,
    Unsafe,
    Timing,
    Activation
}

[Serializable, NetSerializable]
public enum STNukeLayers : byte
{
    Deployed,
    Unsafe,
    Timing,
    Activation
}
