using Content.Client._RMC14.Buckle;
using Content.Client._RMC14.Sprite;
using Content.Shared._RMC14.Sprite;
using Content.Shared._Stories.CombatMech;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using DrawDepthType = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Stories.CombatMech;

public sealed class CombatMechPilotVisualsSystem : EntitySystem
{
    [Dependency] private readonly RMCSpriteSystem _rmcSprite = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(RMCSpriteSystem));

        SubscribeLocalEvent<InsideCombatVehicleComponent, GetDrawDepthEvent>(
            OnInsideVehicleGetDrawDepth,
            after: [typeof(RMCBuckleVisualsSystem)]);
        SubscribeLocalEvent<BuckleComponent, GetDrawDepthEvent>(
            OnBuckledPilotGetDrawDepth,
            after: [typeof(RMCBuckleVisualsSystem)]);
    }

    private void OnInsideVehicleGetDrawDepth(Entity<InsideCombatVehicleComponent> ent, ref GetDrawDepthEvent args)
    {
        args.DrawDepth = DrawDepthType.Mobs;
    }

    private void OnBuckledPilotGetDrawDepth(Entity<BuckleComponent> ent, ref GetDrawDepthEvent args)
    {
        if (ent.Comp.BuckledTo is { } vehicle &&
            TryComp(vehicle, out CombatMechComponent? mech) &&
            mech.PilotEntity == ent.Owner)
        {
            args.DrawDepth = DrawDepthType.Mobs;
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CombatMechComponent>();
        while (query.MoveNext(out var uid, out var mech))
        {
            if (mech.PilotEntity is { } pilot && !Deleted(pilot) && IsPilotInMech(pilot, uid))
            {
                _rmcSprite.UpdateDrawDepth(pilot);

                if (TryComp(pilot, out SpriteComponent? pilotSprite))
                    pilotSprite.RenderOrder = (uint) mech.PilotRenderOrder;
            }

            if (mech.BodyOverlayEntity is { } overlay && !Deleted(overlay) &&
                TryComp(overlay, out SpriteComponent? overlaySprite))
            {
                overlaySprite.RenderOrder = (uint) mech.BodyOverlayRenderOrder;
            }
        }
    }

    private bool IsPilotInMech(EntityUid pilot, EntityUid mech)
    {
        if (TryComp(pilot, out InsideCombatVehicleComponent? inside) &&
            inside.Vehicle != mech)
        {
            return false;
        }

        if (TryComp(pilot, out BuckleComponent? buckle) &&
            buckle.BuckledTo is { } buckledTo &&
            buckledTo != mech)
        {
            return false;
        }

        return true;
    }
}
