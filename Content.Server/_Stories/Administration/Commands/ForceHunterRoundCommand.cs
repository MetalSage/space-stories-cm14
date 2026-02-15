using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stories.Hunter.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed class ForceHunterRoundCommand : IConsoleCommand
{
    public string Command => "forcehuntermode";

    public string Description =>
        "Принудительно включает режим охоты на текущий раунд (загружает карту и открывает слоты).";

    public string Help => "forcehuntermode";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var hunterSystem = entityManager.System<HunterSystem>();

        if (hunterSystem.IsHuntRound)
        {
            shell.WriteLine("Режим охоты уже активен.");
            return;
        }

        hunterSystem.ForceHuntRound();
        shell.WriteLine("Режим охоты принудительно активирован! Карта загружена, слоты открыты.");
    }
}
