using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared._Stories.Security;

[Prototype]
public sealed partial class LawChargePrototype : IPrototype
{
    [ViewVariables, IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; set; } = string.Empty;

    [DataField]
    public string Description { get; private set; } = string.Empty;

    [DataField("details")]
    public PunishmentDetails PunishmentDetails = new PunishmentDetails();
}

[DataDefinition]
public sealed partial class PunishmentDetails
{
    [DataField]
    public TimeSpan ConfinementTime;

    [DataField]
    public bool PermaConfinement = false;

    [DataField]
    public bool Execution = false;

    [DataField]
    public bool IdTermination = false;

}
