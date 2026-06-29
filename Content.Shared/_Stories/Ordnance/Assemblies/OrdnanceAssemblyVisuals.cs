using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Ordnance.Assemblies;

[Serializable, NetSerializable]
public enum OrdnanceAssemblyVisuals
{
    LeftId,
    RightId
}

[Serializable, NetSerializable]
public enum OrdnanceAssemblyLayers
{
    Base,
    Left,
    Right
}
