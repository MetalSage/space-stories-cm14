using Content.Shared.Tag;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.AntiGrief.NewPlayerProtect;

[RegisterComponent, NetworkedComponent]
public sealed partial class NewPlayerProtectComponent : Component
{
	[DataField]
	public ProtoId<TagPrototype> GrenadeTag = "Grenade";

	[DataField]
	public ProtoId<AlertPrototype> AlertProto = "STNewPlayerProtectAlert";

	[DataField]
	public float Hours = 2f;
}
