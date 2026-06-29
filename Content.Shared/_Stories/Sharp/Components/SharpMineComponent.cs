using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpMineComponent : Component
{
    [DataField, AutoNetworkedField]
    public float TriggerRadius = 1.0f;

    [DataField, AutoNetworkedField]
    public float ActivationDelay = 3f;

    [DataField, AutoNetworkedField]
    public float LevelUpInterval = 30f;

    [DataField, AutoNetworkedField]
    public float MaxLifespan = 300f;

    [DataField, AutoNetworkedField]
    public float DisarmDuration = 3f;

    [DataField]
    public DamageSpecifier DetonateOnDamage = new();

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public int MaxLevel = 4;

    [DataField, AutoNetworkedField]
    public bool BlockFriendlyFire = true;

    [DataField]
    public EntProtoId? DisarmSpawnProto;

    public TimeSpan ActivateAt;
    public bool Activated;
    public bool Detonated;
    public SharpMineState AppearanceState = SharpMineState.Inactive;
    public readonly HashSet<EntProtoId<IFFFactionComponent>> IffFactions = new();
}
