using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Chemistry;

[Serializable, NetSerializable]
public enum RMCTankVisuals : byte
{
    TransferDirection
}

[Serializable, NetSerializable]
public enum RMCTankVisualLayers : byte
{
    Meter,
    TransferMode
}
