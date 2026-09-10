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

    /// <summary>
    /// Whether recipients must be visible to the issuer.
    /// </summary>
    [DataField]
    public bool CheckVisibility = true;

    /// <summary>
    /// Role-specific phrases to use instead of the authority's default callouts.
    /// </summary>
    [DataField]
    public List<LocId> Callouts = new();

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
