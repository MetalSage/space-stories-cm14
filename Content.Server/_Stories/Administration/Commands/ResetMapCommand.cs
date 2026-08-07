using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Stories.GameTicking.Commands
{
    [AdminCommand(AdminFlags.Round)]
    sealed class ResetMapCommand : IConsoleCommand
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IGameMapManager _gameMapManager = default!;

        public string Command => "resetmap";
        public string Description => Loc.GetString("stories-command-resetmap-description");
        public string Help => Loc.GetString("stories-command-resetmap-help");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            if (ticker.CanUpdateMap())
            {
                _configurationManager.SetCVar(CCVars.GameMap, "");

                _gameMapManager.ClearSelectedMap();
                ticker.UpdateInfoText();
                shell.WriteLine(Loc.GetString("stories-command-resetmap-success"));
            }
            else
            {
                shell.WriteLine(Loc.GetString("stories-command-resetmap-failed"));
            }
        }
    }
}
