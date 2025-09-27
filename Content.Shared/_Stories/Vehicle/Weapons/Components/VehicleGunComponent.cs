using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleGunComponent : Component
{
	[DataField, AutoNetworkedField]
	public bool NeedHands;

	[DataField, AutoNetworkedField]
	public float DisableAtHullDamage = -1f;

	[DataField, AutoNetworkedField]
	public EntityUid? User;
}