using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Synth.WorkingJoe;

[Prototype("stWorkingJoeAppearance")]
public sealed partial class STWorkingJoeAppearancePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    [DataField(required: true)]
    public LocId NamePrefix;

    [DataField]
    public Color EyeColor = Color.FromHex("#00FF00");

    [DataField]
    public Color SkinColor = Color.White;
}
