using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server._Stories.Administration;

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
public sealed class MobStateCommand : ToolshedCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    [CommandImplementation("is")]
    public EntityUid? Is(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid ent,
        [CommandArgument] MobState targetState)
    {
        if (!_entityManager.TryGetComponent<MobStateComponent>(ent, out var mobStateComponent))
            return null;

        return mobStateComponent.CurrentState == targetState ? ent : null;
    }

    [CommandImplementation("is")]
    public IEnumerable<EntityUid> Is(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> ents,
        [CommandArgument] MobState targetState)
    {
        return ents.Select(ent => Is(ctx, ent, targetState)).OfType<EntityUid>();
    }
}
