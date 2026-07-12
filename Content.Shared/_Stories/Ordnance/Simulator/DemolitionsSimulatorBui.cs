using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Simulator;

[Serializable, NetSerializable]
public enum DemolitionsSimulatorUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class DemolitionsSimulatorDetonateMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DemolitionsSimulatorResetMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DemolitionsSimulatorEjectMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DemolitionsSimulatorSwitchCategoryMessage(string category) : BoundUserInterfaceMessage
{
    public string Category = category;
}

[Serializable, NetSerializable]
public sealed class DemolitionsSimulatorSwitchProtoMessage(EntProtoId prototype) : BoundUserInterfaceMessage
{
    public EntProtoId Prototype = prototype;
}
