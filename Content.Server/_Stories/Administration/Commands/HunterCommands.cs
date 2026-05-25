using Content.Server._Stories.Hunter;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Stories.Administration.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Round)]
public sealed class HunterForceCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        system.IsHuntRound = true;
        ctx.WriteLine(Loc.GetString("stories-command-hunter-force-success"));
    }
}

[ToolshedCommand, AdminCommand(AdminFlags.Round)]
public sealed class HunterDisableCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        system.IsHuntRound = false;
        ctx.WriteLine(Loc.GetString("stories-command-hunter-disable-success"));
    }
}

[ToolshedCommand, AdminCommand(AdminFlags.Round)]
public sealed class HunterCheckCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        ctx.WriteLine(Loc.GetString("stories-command-hunter-check", ("isHuntRound", system.IsHuntRound)));
    }
}
