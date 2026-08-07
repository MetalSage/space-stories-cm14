using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Medical.Components;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterHealingGunComponent : Component
{
    [DataField]
    public EntProtoId AmmoPrototype = "STHealingGel";

    [DataField]
    public string EmptyState = "healing_gun_empty";

    [DataField] [AutoNetworkedField]
    public bool Loaded = true;

    [DataField]
    public string LoadedState = "healing_gun";

    [DataField]
    public SoundSpecifier ReloadSound = new SoundPathSpecifier("/Audio/_RMC14/Medical/air_release.ogg");
}
