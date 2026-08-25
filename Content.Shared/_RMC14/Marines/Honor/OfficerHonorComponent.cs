using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Honor;

/// <summary>
/// Allows an officer to call nearby marines to attention.
/// </summary>
[RegisterComponent]
public sealed partial class OfficerHonorComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionOfficerHonor";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public int Range = 10;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextHonorAt;
}

[RegisterComponent]
public sealed partial class OfficerHonorForcedWhisperComponent : Component
{
    public TimeSpan ExpiresAt;
}

/// <summary>
/// Forces ordinary IC speech to be sent as a whisper while a superior's silence order is active.
/// </summary>
[RegisterComponent]
public sealed partial class MarineSilencedForcedWhisperComponent : Component
{
    public TimeSpan ExpiresAt;
}
