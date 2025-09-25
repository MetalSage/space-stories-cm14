using System.Numerics;
using Robust.Shared.Utility;
using Robust.Shared.Maths;

namespace Content.Client._Stories.Vehicle.Attachables;

[RegisterComponent]
public sealed partial class VehicleAttachableDamageVisualsComponent : Component
{
	[DataField]
	public float DarknessLevel = 0.6f;
}
