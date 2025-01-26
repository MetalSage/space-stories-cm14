using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Empower;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoEmpowerSystem))]
public sealed partial class XenoEmpowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 50;

    [DataField, AutoNetworkedField]
    public float Range = 4.5f;

    [DataField, AutoNetworkedField]
    public float MaxTargets = 6; // it's 6 but for cycle...

    [DataField, AutoNetworkedField]
    public FixedPoint2 AmountBase = 50;

    [DataField, AutoNetworkedField]
    public FixedPoint2 AmountPerHuman = 50;

    [DataField, AutoNetworkedField]
    public TimeSpan EmpowerOffAt;

    [DataField, AutoNetworkedField]
    public TimeSpan ShieldOffAt;

    [DataField, AutoNetworkedField]
    public TimeSpan FirstActiveOffAt;

    [DataField, AutoNetworkedField]
    public TimeSpan EmpowerDuration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public TimeSpan ShieldDuration = TimeSpan.FromSeconds(15);

    [DataField, AutoNetworkedField]
    public TimeSpan FirstActiveDuration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public bool EmpowerActive = false;

    [DataField, AutoNetworkedField]
    public bool FirstActive = false;

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "RMCEffectEmpower";

    [DataField, AutoNetworkedField]
    public EntProtoId EffectOnMarine = "RMCEffectEmpowerAlert";

    // TODO RMC14 extra sound on impact
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_roar1.ogg");

}
