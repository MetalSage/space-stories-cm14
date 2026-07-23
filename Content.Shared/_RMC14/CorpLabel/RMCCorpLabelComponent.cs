using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.CorpLabel;

/// <summary>
/// Adds a manufacturer logo line to this item's examine text, matching CMSS13's
/// <c>/datum/element/corp_label</c> (e.g. items stamped with Weyland-Yutani, Seegson, Hyperdyne).
/// Purely cosmetic flavor text.
/// </summary>
[RegisterComponent]
public sealed partial class RMCCorpLabelComponent : Component
{
    [DataField(required: true)]
    public LocId Manufacturer;
}
