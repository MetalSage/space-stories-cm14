using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Xenonids.WarriorBulwark.ReflectiveShield;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReflectiveShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public Angle ReflectAngle = Angle.FromDegrees(50);

    [DataField, AutoNetworkedField]
    public float ReflectChance = 1f;

    [DataField, AutoNetworkedField]
    public float ReflectionMultiplier = 0.5f;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public TimeSpan MinCooldown = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan FullCooldown = TimeSpan.FromSeconds(18);

    [DataField, AutoNetworkedField]
    public float CooldownPerSecond = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan? DeactivateAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? ActivatedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? PendingCooldown;
}
