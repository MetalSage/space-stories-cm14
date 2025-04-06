using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.AcidMine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(RMCXenoDeployAcidMineSystem))]
public sealed partial class RMCXenoAcidMineComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Attached;

    [DataField, AutoNetworkedField]
    public TimeSpan Activation;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.35f);

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public DamageSpecifier BarricadeDamage = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ReduceDelayPerHit = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public bool Activated;

    [DataField]
    public ComponentRegistry? Add;
}
