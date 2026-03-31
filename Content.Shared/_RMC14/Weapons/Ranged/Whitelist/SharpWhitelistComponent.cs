using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged.Whitelist;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMGunSystem))]
[SpecialistSkillComponent("SHARP")]
public sealed partial class SharpWhitelistComponent : Component;
