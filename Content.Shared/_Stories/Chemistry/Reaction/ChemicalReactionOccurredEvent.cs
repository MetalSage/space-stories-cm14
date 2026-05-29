using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Chemistry.Reaction;

[ByRefEvent]
public readonly record struct ChemicalReactionOccurredEvent(ReactionPrototype Reaction, Entity<SolutionComponent> Solution, FixedPoint2 UnitReactions);
