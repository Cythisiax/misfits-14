using Content.Server._Misfits.WastelandMap;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>Places a named, round-scoped global GPS marker at the administrator's position.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class MapPinCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "mappin";
    public string Description => "Places or removes round-scoped global tactical-map pins at your current position.";
    public string Help => "mappin [remove]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && !args[0].Equals("remove", StringComparison.OrdinalIgnoreCase)))
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } actor)
        {
            shell.WriteError("You must have an attached in-game entity to place a map pin.");
            return;
        }

        var mapPins = _systems.GetEntitySystem<MapPinSystem>();
        var opened = args.Length == 1 ? mapPins.TryOpenManager(actor) : mapPins.TryOpenNamePrompt(actor);
        if (!opened)
            shell.WriteError("Could not open the map pin prompt.");
    }
}
