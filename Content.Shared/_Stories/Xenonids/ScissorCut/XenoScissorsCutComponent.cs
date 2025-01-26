using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared._RMC14.Xenonids.ScissorsCut;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoScissorsCutSystem))]
public sealed partial class XenoScissorsCutComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PlasmaCost = 25;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage;

    [DataField, AutoNetworkedField]
    public int AP = 20;

    [DataField, AutoNetworkedField]
    public int? MaxTargets;

    [DataField, AutoNetworkedField]
    public EntProtoId AttackEffect = "RMCEffectExtraSlash";

    [DataField, AutoNetworkedField]
    public EntProtoId EffectSlowdown = "RMCEffectRavagerSlow";

    [DataField, AutoNetworkedField]
    public TimeSpan Slowdown = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public SoundSpecifier Roar = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_roar3.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_claw_flesh3.ogg");

    [DataField, AutoNetworkedField]
    public TimeSpan? EmoteCooldown = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public FixedPoint2 Range = FixedPoint2.New(4);

    [DataField, AutoNetworkedField]
    public int RechargeTargetsRequired = 2;
}
