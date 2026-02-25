using System;
using Robust.Shared.GameObjects;

namespace Content.Server._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpStickyDartRuntimeComponent : Component
{
    public bool Armed;
    public TimeSpan DetonateAt;
}
