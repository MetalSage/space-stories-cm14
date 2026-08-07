using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.XenoBoxer.BoxerJab;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BoxerJabSystem))]
public sealed partial class BoxerJabComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("Punch");

    [DataField, AutoNetworkedField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public float KnockoutIncrease = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan RootTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public EntProtoId RootEffect = "CMEffectPunch";

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.2);
}
