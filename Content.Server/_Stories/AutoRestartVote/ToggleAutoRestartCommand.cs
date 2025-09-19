using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stories.AutoRestartVote;

[AdminCommand(AdminFlags.Round)]
public sealed class ToggleAutoRestartCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _e = default!;

    public string Command => "toggleautorestart";
    public string Description => string.Empty;
    public string Help => string.Empty;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ticker = _e.System<GameTicker>();

        if (ticker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteLine("This can only be executed while the game is in a round");
            return;
        }

        var result = _e.System<AutoRestartVoteSystem>().ToggleAutoRestart();
        shell.WriteLine(result ? "AutoRestart Toggled" : "AutoRestart Untoggled");
    }
}
