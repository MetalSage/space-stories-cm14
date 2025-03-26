using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stories;

/// <summary>
/// Skins
/// </summary>
[Prototype("xenoSkin")]
[Serializable, NetSerializable]
public sealed partial class XenoSkinsPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField("name")]
    public string Name = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField("rsi")]
    public ResPath Rsi;

    [ViewVariables(VVAccess.ReadWrite), DataField("state")]
    public string State = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public ProtoId<JobPrototype> Xeno;
}
