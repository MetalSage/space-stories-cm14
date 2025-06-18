using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Sponsors.XenoSkins;

[Prototype("xenoSkin"), Serializable, NetSerializable]
public sealed partial class XenoSkinsPrototype : IPrototype
{
    /// <summary>
    /// The prototype ID.
    /// A unique key used to identify this skin within the system.
    /// </summary>
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// The name of the skin.
    /// The display name for the skin, used in the user interface.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("name")]
    public string Name = string.Empty;

    /// <summary>
    /// The path to the RSI sprite resource.
    /// Used for displaying the appearance of the xeno with this skin.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("spriteRsi")]
    public ResPath SpriteRsi;

    /// <summary>
    /// The type of xeno this skin is designed for.
    /// Links to the prototype of the job (e.g. for a xeno queen - CMXenoQueen)
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public ProtoId<JobPrototype> Xeno;
}
