using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Localization;

namespace Content.Server._RMC14.Vehicle;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class VehicleSupplyCommand : ToolshedCommand
{
    [CommandImplementation("addstorage")]
    public void AddStorage(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] string vehicleId)
    {
        var system = Sys<VehicleSupplySystem>();
        if (!system.TryGetAnyLift(out var lift))
        {
            // Stories-Start
            ctx.WriteLine(Robust.Shared.Localization.Loc.GetString("rmc-vehicle-supply-cmd-no-lift"));
            // Stories-End
            return;
        }

        AddStorageInternal(ctx, system, lift.Owner, vehicleId);
    }

    [CommandImplementation("addstoragelift")]
    public void AddStorage(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] string vehicleId,
        [CommandArgument] EntityUid lift)
    {
        var system = Sys<VehicleSupplySystem>();
        AddStorageInternal(ctx, system, lift, vehicleId);
    }

    private static void AddStorageInternal(
        IInvocationContext ctx,
        VehicleSupplySystem system,
        EntityUid liftUid,
        string vehicleId)
    {
        if (system.DebugAddVehicleToStorage(liftUid, vehicleId, true, out var reason))
        {
            system.DebugEnsureVehicleInConsoles(liftUid, vehicleId);
            // Stories-Start
            ctx.WriteLine(Robust.Shared.Localization.Loc.GetString("rmc-vehicle-supply-cmd-success", ("vehicle", vehicleId)));
            // Stories-End
        }
        else
        {
            // Stories-Start
            ctx.WriteLine(reason ?? Robust.Shared.Localization.Loc.GetString("rmc-vehicle-supply-cmd-fail"));
            // Stories-End
        }
    }
}
