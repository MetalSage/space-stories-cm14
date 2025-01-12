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
    public float Range = 4;

    [DataField, AutoNetworkedField]
    public float MaxTargets = 6;

    [DataField, AutoNetworkedField]
    public FixedPoint2 AmountBase = 50;

    [DataField, AutoNetworkedField]
    public FixedPoint2 AmountPerHuman = 50;

    [DataField, AutoNetworkedField]
    public TimeSpan EmpowerOffAt;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public bool EmpowerActive = false;

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "RMCEffectEmpower";

    // TODO RMC14 extra sound on impact
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_roar1.ogg");

}
