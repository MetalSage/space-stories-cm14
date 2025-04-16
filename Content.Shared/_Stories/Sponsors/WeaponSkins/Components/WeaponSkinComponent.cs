using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Sponsors.WeaponSkins.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeaponSkinComponent : Component
{
    /// <summary>
    /// The ID of the default skin for this weapon. Must be a key in the Skins dictionary.
    /// </summary>
    [DataField("defaultSkin", required: true), ViewVariables(VVAccess.ReadWrite)]
    public string DefaultSkin = "Default";

    /// <summary>
    /// Dictionary mapping unique skin IDs (strings) to their RSI paths.
    /// </summary>
    [DataField("skins", required: true), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, ResPath> Skins = default!;

    /// <summary>
    /// Optional mapping of layer keys to skin-specific states for those layers.
    /// </summary>
    [DataField("layers"), AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, Dictionary<string, string>>? Layers;
}
