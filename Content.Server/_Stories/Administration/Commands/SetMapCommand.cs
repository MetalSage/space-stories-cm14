using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.GameTicking.Commands
{
    [AdminCommand(AdminFlags.Round)]
    sealed class SetMapCommand : IConsoleCommand
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IGameMapManager _gameMapManager = default!;

        public string Command => "setmap";
        public string Description => Loc.GetString("stories-command-setmap-description");
        public string Help => Loc.GetString("stories-command-setmap-help");

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteLine(Loc.GetString("stories-command-setmap-need-one-argument"));
                return;
            }

            var name = args[0];

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            if (ticker.CanUpdateMap())
            {
                _configurationManager.SetCVar(CCVars.GameMap, "");

                _gameMapManager.SelectMap(name);
                ticker.UpdateInfoText();
                shell.WriteLine(Loc.GetString("stories-command-setmap-success", ("map", name)));
            }
            else
            {
                shell.WriteLine(Loc.GetString("stories-command-setmap-failed", ("map", name)));
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                var options = IoCManager.Resolve<IPrototypeManager>()
                    .EnumeratePrototypes<GameMapPrototype>()
                    .Select(p => new CompletionOption(p.ID, p.MapName))
                    .OrderBy(p => p.Value);

                return CompletionResult.FromHintOptions(options, Loc.GetString("stories-command-setmap-arg-map"));
            }

            return CompletionResult.Empty;
        }
    }
}
