using System.Numerics;
using Robust.Shared.Utility;
using Robust.Shared.Maths;

namespace Content.Client._Stories.APC.Attachables;

[RegisterComponent]
public sealed partial class APCAttachableDamageVisualsComponent : Component
{
	[DataField]
	public float DarknessLevel = 0.8f;
}
