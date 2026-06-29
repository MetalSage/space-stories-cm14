using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Interaction
{
    [RegisterComponent]
    public sealed partial class ExtendedInteractionRangeComponent : Component
    {
        [DataField("range")]
        public float Range = 3.0f;
    }
}
