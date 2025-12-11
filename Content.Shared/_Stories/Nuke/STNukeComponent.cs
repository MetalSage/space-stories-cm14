using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Nuke;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STNukeComponent : Component
{
	[DataField, AutoNetworkedField]
	public TimeSpan DecryptionTime = TimeSpan.FromMinutes(10);

	[DataField, AutoNetworkedField]
	public TimeSpan DetonationTime = TimeSpan.FromMinutes(3);

	[DataField, AutoNetworkedField]
	public TimeSpan PenaltionTime = TimeSpan.FromMinutes(2);

	[DataField, AutoNetworkedField]
	public TimeSpan? DecryptionOn;

	[DataField, AutoNetworkedField]
	public TimeSpan? ExplodeOn;

	[DataField, AutoNetworkedField]
	public bool Safety = true;

	[DataField, AutoNetworkedField]
	public int RequiredTowers = 2;
}
