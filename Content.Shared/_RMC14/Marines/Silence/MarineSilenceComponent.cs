using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Silence;

/// <summary>
/// Allows a marine in a command role to order lower-ranked marines to be silent.
/// </summary>
[RegisterComponent]
public sealed partial class MarineSilenceComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionMarineSilence";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public MarineSilenceAuthority Authority = MarineSilenceAuthority.Officer;

    [DataField]
    public int Range = 10;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(22);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextSilenceAt;
}

public enum MarineSilenceAuthority : byte
{
    Officer,
    Sergeant,
    MilitaryPolice,
}
