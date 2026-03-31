using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMGunSystem))]
[SpecialistSkillComponent("SHARP")]
public sealed partial class SharpWhitelistComponent : Component;
