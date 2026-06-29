using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpSpecialistVendorComponent : Component
{
    [DataField(required: true)]
    public EntProtoId EquipmentCase = default!;

    [DataField(required: true)]
    public LocId SpecialistRole = string.Empty;

    [DataField]
    public LocId? SpecialistPrefix;
}
