using Content.Client._RMC14.Buckle;
using Content.Shared._RMC14.Sprite;
using Content.Shared._Stories.CombatMech;
using DrawDepthType = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Stories.CombatMech;

public sealed class CombatMechPilotVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<InsideCombatVehicleComponent, GetDrawDepthEvent>(
            OnInsideVehicleGetDrawDepth,
            after: [typeof(RMCBuckleVisualsSystem)]);
    }

    private void OnInsideVehicleGetDrawDepth(Entity<InsideCombatVehicleComponent> ent, ref GetDrawDepthEvent args)
    {
        if (!HasComp<CombatMechComponent>(ent.Comp.Vehicle))
            return;

        args.DrawDepth = DrawDepthType.Mobs;
    }
}
