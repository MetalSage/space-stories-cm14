using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     Marks a melee weapon as applying <see cref="BreacherHammerStacksComponent"/> to xenos it hits.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BreacherHammerSystem))]
public sealed partial class BreacherHammerComponent : Component
{
    /// <summary>
    ///     If true, hitting an incapacitated target always triggers the knockdown immediately,
    ///     regardless of grip or accumulated stacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InstantOnIncapacitated = true;

    /// <summary>
    ///     If set, this hammer is tethered to this entity -- whenever it isn't in that entity's
    ///     hands (dropped, stolen, knocked away), it steadily drags itself back instead of
    ///     teleporting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TetherOwner;

    /// <summary>
    ///     Reference distance used to scale the pull speed -- at this distance the hammer is
    ///     already pulling at <see cref="TetherMaxSpeed"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TetherMaxDistance = 1.5f;

    /// <summary>
    ///     Stop pulling once this close, so it doesn't jitter in place right next to the owner.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TetherStopDistance = 0.3f;

    [DataField, AutoNetworkedField]
    public float TetherBaseSpeed = 4f;

    [DataField, AutoNetworkedField]
    public float TetherMaxSpeed = 6f;

    /// <summary>
    ///     Beyond this distance the tether just snaps outright instead of continuing to pull --
    ///     covers cases like getting thrown/hijacked far away faster than the pull can keep up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TetherBreakDistance = 6.5f;
}
