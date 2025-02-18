using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._Stories.Xenonids.WarriorBoxer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KOComponent : Component
{
    [DataField, AutoNetworkedField]
    public float KOCounter;

    [DataField, AutoNetworkedField]
    public TimeSpan KOResetTime = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public float KOIncreasePerMeleeHit = 0.5f;

    [DataField, AutoNetworkedField]
    public float MaxKOCounter = 15f;

    [DataField, AutoNetworkedField]
    public EntityUid? LastHitTarget;

    public TimeSpan LastHitTime;
}
