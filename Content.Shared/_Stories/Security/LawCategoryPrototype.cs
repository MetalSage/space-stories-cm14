using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Security;

[Prototype]
public sealed partial class LawCategoryPrototype : IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; set; } = string.Empty;

    [DataField]
    public HashSet<ProtoId<LawChargePrototype>> Tags = default!;
}
