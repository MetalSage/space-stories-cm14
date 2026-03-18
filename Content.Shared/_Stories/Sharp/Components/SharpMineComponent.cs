using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpMineComponent : Component
{
    [DataField, AutoNetworkedField]
    public float TriggerRadius = 1.0f;

    [DataField, AutoNetworkedField]
    public float LevelUpInterval = 75f;

    [DataField, AutoNetworkedField]
    public float MaxLifespan = 300f;

    [DataField, AutoNetworkedField]
    public float StickyDartGracePeriod = 0.25f;

    [DataField]
    public DamageSpecifier DetonateOnDamage = new();

    [DataField]
    public float ExplosionRadius;

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public int MaxLevel = 4;

    [DataField, AutoNetworkedField]
    public bool IgnoreAnyIff = true;
}
