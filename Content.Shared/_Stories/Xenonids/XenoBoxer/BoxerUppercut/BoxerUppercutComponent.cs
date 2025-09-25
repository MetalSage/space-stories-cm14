using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerUppercut;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BoxerUppercutSystem))]
public sealed partial class BoxerUppercutComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 3f;

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public SoundSpecifier ClawSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_block.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier GongSound = new SoundPathSpecifier("/Audio/_Stories/Effects/dingding.ogg");

    [DataField, AutoNetworkedField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public TimeSpan UnconsciousTime = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public float HealPerStack = 0.05f;

    [DataField, AutoNetworkedField]
    public float DamageModificator = 10f;

    [DataField, AutoNetworkedField]
    public float MaxDamage = 150f;

    [DataField, AutoNetworkedField]
    public EntProtoId HealEffect = "RMCEffectHealQueen";
}
