using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Empower;
using Content.Shared.Stunnable;
using Content.Shared.Coordinates;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.RavageCharge;

public sealed class XenoRavageChargeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrownItemSystem _thrownItem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly RMCPullingSystem _rmcPulling = default!;
    private EntityQuery<XenoEmpowerComponent> _empower;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();
        _empower = GetEntityQuery<XenoEmpowerComponent>();

        SubscribeLocalEvent<XenoRavageChargeComponent, XenoRavageChargeActionEvent>(OnXenoRavageChargeAction);
        SubscribeLocalEvent<XenoRavageChargeComponent, ThrowDoHitEvent>(OnXenoRavageChargeHit);
    }

    private void OnXenoRavageChargeAction(Entity<XenoRavageChargeComponent> xeno, ref XenoRavageChargeActionEvent args)
    {
        if (args.Handled)
            return;

        var attempt = new XenoRavageChargeAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return;

        _rmcPulling.TryStopAllPullsFromAndOn(xeno);

        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(args.Target);
        var diff = target.Position - origin.Position;
        diff = diff.Normalized() * xeno.Comp.Range;

        xeno.Comp.Charge = diff;
        Dirty(xeno);

        _throwing.TryThrow(xeno, diff, 20, animated: false);

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Sound, xeno);

    }

    private void OnXenoRavageChargeHit(Entity<XenoRavageChargeComponent> xeno, ref ThrowDoHitEvent args)
    {
        // TODO RMC14 lag compensation
        var targetId = args.Target;
        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);
            _thrownItem.StopThrow(xeno, thrown);
        }


        if(_empower.TryGetComponent(xeno, out var empower))
        {
            if(empower.EmpowerActive)
            {
                _rmcPulling.TryStopAllPullsFromAndOn(targetId);

                var origin = _transform.GetMapCoordinates(xeno);
                var target = _transform.GetMapCoordinates(targetId);
                var diff = target.Position - origin.Position;
                diff = diff.Normalized() * xeno.Comp.Range;

                _stun.TryParalyze(targetId, xeno.Comp.StunTime, true);
                _throwing.TryThrow(targetId, diff, 5);
            }
        }
    }
}
