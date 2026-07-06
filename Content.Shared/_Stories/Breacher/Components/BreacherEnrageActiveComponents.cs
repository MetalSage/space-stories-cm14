using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     Applied to a marine while their Breacher armor's Enrage ability is active.
///     Removed automatically by <see cref="BreacherArmorSystem"/> once <see cref="EndTime"/> passes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BreacherArmorSystem))]
public sealed partial class BreacherEnrageActiveComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    ///     The armor entity that granted this, so its <see cref="BreacherArmorComponent.EnrageDamageResist"/>
    ///     key can be removed again on expiry.
    /// </summary>
    [DataField]
    public EntityUid Armor;

    [DataField]
    public TimeSpan NextPulse;
}
