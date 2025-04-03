using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Sponsors.WeaponSkins.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SprayPaintComponent : Component
{
    /// <summary>
    /// The ID of the skin this spray paint applies. Must match a key in a target WeaponSkinComponent.
    /// </summary>
    [DataField("skinId", required: true), ViewVariables(VVAccess.ReadWrite)]
    public string SkinId = default!;

    /// <summary>
    /// How long it takes to apply the paint.
    /// </summary>
    [DataField("applyDuration"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ApplyDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Number of uses. If null, infinite uses (usually not desired).
    /// Set to 1 to consume after one use.
    /// </summary>
    [DataField("uses"), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int? Uses = 1;
}
