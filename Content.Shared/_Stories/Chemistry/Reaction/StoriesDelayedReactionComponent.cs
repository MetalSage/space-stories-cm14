using System;
using System.Collections.Generic;
using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Chemistry.Reaction;

[RegisterComponent, NetworkedComponent]
public sealed partial class StoriesDelayedReactionComponent : Component
{
    [DataField]
    public List<PendingReaction> PendingReactions = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public partial struct PendingReaction
{
    [DataField]
    public StoriesChemReactionFlags Flag;

    [DataField]
    public TimeSpan TriggerAt;

    [DataField]
    public Solution Payload;

    [DataField]
    public string ReactionId;
}
