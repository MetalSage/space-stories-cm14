using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Marines;
using Content.Shared.Construction.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.UnanchorHijackBarricades;

public sealed class UnanchorHijackBarricadesSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipHijackStartEvent>(OnHijackStarted);
        SubscribeLocalEvent<BarricadeComponent, AnchorAttemptEvent>(OnBarricadeAnchorAttempt);
    }

    private void OnHijackStarted(ref DropshipHijackStartEvent args)
    {
        var almayerQuery = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (almayerQuery.MoveNext(out _, out var xform))
        {
            var mapId = xform.MapID;

            var barricadeQuery = EntityQueryEnumerator<BarricadeComponent, TransformComponent>();
            while (barricadeQuery.MoveNext(out var uid, out var _, out var barricadeXform))
            {
                if (barricadeXform.MapID == mapId)
                    _transform.Unanchor(uid);
            }
        }
    }

    private void OnBarricadeAnchorAttempt(Entity<BarricadeComponent> barricade, ref AnchorAttemptEvent args)
    {
        var distressQuery = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (distressQuery.MoveNext(out var uid, out var distress))
        {
            if (distress.Hijack)
            {
                args.Cancel();
                return;
            }
        }
    }
}