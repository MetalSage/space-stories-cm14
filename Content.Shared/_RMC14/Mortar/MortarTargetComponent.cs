using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Mortar;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedMortarSystem))]
public sealed partial class MortarTargetComponent : Component;
