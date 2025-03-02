using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;


namespace Content.Shared._Stories.AcidBlood;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class CMAcidBloodComponent : Component
{

    [DataField, AutoNetworkedField]
    public EntProtoId ProjectileId = "XenoAcidBloodProjectile";
}
