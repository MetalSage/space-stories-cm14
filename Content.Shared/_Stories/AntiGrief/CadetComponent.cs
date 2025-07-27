using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.AntiGrief.Cadet;

[RegisterComponent, NetworkedComponent]
public sealed partial class CadetComponent : Component
{
	[DataField]
	public ProtoId<TagPrototype> GrenadeTag = "Grenade";
}
