using System;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpMineRuntimeComponent : Component
{
    public TimeSpan SpawnTime;
}
