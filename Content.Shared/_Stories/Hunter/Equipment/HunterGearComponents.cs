using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Equipment;

[RegisterComponent] [NetworkedComponent]
public sealed partial class HunterGearTrackableComponent : Component
{
}

[RegisterComponent] [NetworkedComponent]
public sealed partial class HunterCleanerVialComponent : Component
{
    [DataField]
    public EntProtoId AcidPrototype = "STHunterDissolvingGel";

    [DataField]
    public SoundSpecifier DissolveSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/acid_sizzle4.ogg");

    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MeltTime = TimeSpan.FromSeconds(2);

    [DataField]
    public XenoAcidStrength Strength = XenoAcidStrength.Hunter;
}
