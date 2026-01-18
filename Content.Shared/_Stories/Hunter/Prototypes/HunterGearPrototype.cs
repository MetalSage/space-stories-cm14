using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Hunter.Prototypes;

[Prototype("hunterGear")]
public sealed partial class HunterGearPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Category for the editor tab (e.g., Armor, Mask, Greaves, Caster, Bracer, Accessory, Cape).
    /// </summary>
    [DataField("category", required: true)]
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Material sub-group (e.g., Standard, Ebony, Gold). Used for sorting in tabs.
    /// </summary>
    [DataField("material")]
    public string Material { get; private set; } = "Standard";

    /// <summary>
    /// The actual entity prototype ID to spawn.
    /// </summary>
    [DataField("entityProto", required: true)]
    public EntProtoId EntityProto { get; private set; }

    /// <summary>
    /// If true, only sponsors can select this gear.
    /// </summary>
    [DataField("sponsorOnly")]
    public bool SponsorOnly { get; private set; } = false;
    
    /// <summary>
    /// Is this the default item for this category? Used for fallback.
    /// </summary>
    [DataField("isDefault")]
    public bool IsDefault { get; private set; } = false;
}
