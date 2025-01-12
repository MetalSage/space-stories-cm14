using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.RavageCharge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoRavageChargeSystem))]
public sealed partial class XenoRavageChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 25;

    [DataField, AutoNetworkedField]
    public float Range = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(1);

    // TODO RMC14 extra sound on impact
    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_roar1.ogg");

    [DataField, AutoNetworkedField]
    public Vector2? Charge;

}
