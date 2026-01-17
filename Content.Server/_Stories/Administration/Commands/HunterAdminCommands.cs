using Content.Server._Stories.Hunter;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Stories.Administration.Commands;

[ToolshedCommand] [AdminCommand(AdminFlags.Round)]
public sealed class HunterForceCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        system.IsHuntRound = true;
        ctx.WriteLine(
            "Раунд с охотниками принудительно вклюён.");
    }
}

[ToolshedCommand] [AdminCommand(AdminFlags.Round)]
public sealed class HunterDisableCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        system.IsHuntRound = false;
        ctx.WriteLine("Раунд с охотниками принудительно отключён.");
    }
}

[ToolshedCommand] [AdminCommand(AdminFlags.Round)]
public sealed class HunterCheckCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Run(IInvocationContext ctx)
    {
        var system = GetSys<HunterSystem>();
        ctx.WriteLine($"Сейчас раунд с охотниками: {system.IsHuntRound}");
    }
}
