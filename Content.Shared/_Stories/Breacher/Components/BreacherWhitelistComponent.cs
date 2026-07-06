using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     Marks a marine as authorized to use Breacher-exclusive gear (XM52, M6H-BREACHER, N45 hammer,
///     N30 shield, M40 Enrage). Granted automatically on redeeming the equipment case via
///     CMChangeUserOnVend on the case's own prototype. Also admin-grantable via RMCAdminEui,
///     same as other specialist whitelists (e.g. GrenadeSpecWhitelistComponent).
/// </summary>
[RegisterComponent, NetworkedComponent]
[SpecialistSkillComponent("Breacher")]
public sealed partial class BreacherWhitelistComponent : Component;
