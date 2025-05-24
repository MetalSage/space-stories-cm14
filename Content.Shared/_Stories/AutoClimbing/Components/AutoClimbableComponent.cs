using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.AutoClimbing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoClimbableComponent : Component
{
	[DataField, AutoNetworkedField]
	public float CollideCooldown = 0.2f;

	[DataField, AutoNetworkedField]
	public TimeSpan LastCollideTime;
}
