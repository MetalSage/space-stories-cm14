using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Weapons.Ranged.IFF;

namespace Content.Server._Stories.Sharp;

[RegisterComponent]
public sealed partial class SharpMineRuntimeComponent : Component
{
    public TimeSpan ActivateAt;
    public bool Activated;
    public bool? AppearanceEnabled;
    public readonly HashSet<EntProtoId<IFFFactionComponent>> IffFactions = new();
}
