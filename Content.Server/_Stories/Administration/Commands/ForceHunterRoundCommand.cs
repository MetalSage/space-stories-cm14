using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stories.Hunter.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed class ForceHunterRoundCommand : IConsoleCommand
{
    public string Command => "forcehuntermode";

    public string Description => Loc.GetString("stories-command-forcehuntermode-description");

    public string Help => Loc.GetString("stories-command-forcehuntermode-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var hunterSystem = entityManager.System<HunterSystem>();

        if (hunterSystem.IsHuntRound)
        {
            shell.WriteLine(Loc.GetString("stories-command-forcehuntermode-already-active"));
            return;
        }

        hunterSystem.ForceHuntRound();
        shell.WriteLine(Loc.GetString("stories-command-forcehuntermode-success"));
    }
}
