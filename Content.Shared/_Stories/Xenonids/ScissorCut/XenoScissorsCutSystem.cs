using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Empower;
using Content.Shared.Stunnable;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Shared._RMC14.Xenonids.ScissorsCut;

public abstract class SharedXenoScissorsCutSystem : EntitySystem
{
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _plasma = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRMCMeleeWeaponSystem _rmcMelee = default!;

    protected Box2Rotated LastAttack;

    private EntityQuery<XenoEmpowerComponent> _empower;

    private const int AttackMask = (int)(CollisionGroup.MobMask | CollisionGroup.Opaque);

    public override void Initialize()
    {
        _empower = GetEntityQuery<XenoEmpowerComponent>();

        SubscribeLocalEvent<XenoScissorsCutComponent, XenoScissorsCutActionEvent>(OnXenoScissorsCutAction);
    }

    private void OnXenoScissorsCutAction(Entity<XenoScissorsCutComponent> xeno, ref XenoScissorsCutActionEvent args)
    {
        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        //Note below is mostly all tail stab code
        var transform = Transform(xeno);

        var userCoords = _transform.GetMapCoordinates(xeno, transform);
        if (userCoords.MapId == MapId.Nullspace)
            return;

        var targetCoords = _transform.ToMapCoordinates(args.Target);
        if (userCoords.MapId != targetCoords.MapId)
            return;

        var range = xeno.Comp.Range.Float();
        var box = new Box2(userCoords.Position.X - 0.10f, userCoords.Position.Y, userCoords.Position.X + 0.10f, userCoords.Position.Y + range);

        var matrix = Vector2.Transform(targetCoords.Position, _transform.GetInvWorldMatrix(transform));
        var rotation = _transform.GetWorldRotation(xeno).RotateVec(-matrix).ToWorldAngle();
        var boxRotated = new Box2Rotated(box, rotation, userCoords.Position);
        LastAttack = boxRotated;

        // ray on the left side of the box
        var leftRay = new CollisionRay(boxRotated.BottomLeft, (boxRotated.TopLeft - boxRotated.BottomLeft).Normalized(), AttackMask);

        // ray on the right side of the box
        var rightRay = new CollisionRay(boxRotated.BottomRight, (boxRotated.TopRight - boxRotated.BottomRight).Normalized(), AttackMask);

        bool Ignore(EntityUid uid)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, uid))
                return true;

            return false;
        }

        var intersect = _physics.IntersectRayWithPredicate(transform.MapID, leftRay, range, Ignore, false);
        intersect = intersect.Concat(_physics.IntersectRayWithPredicate(transform.MapID, rightRay, range, Ignore, false));
        var results = intersect.Select(r => r.HitEntity).ToHashSet();

        var actualResults = new List<EntityUid>();
        foreach (var result in results)
        {
            if (!_interaction.InRangeUnobstructed(xeno.Owner, result, range: range))
                continue;

            actualResults.Add(result);
            if (xeno.Comp.MaxTargets != null && actualResults.Count >= xeno.Comp.MaxTargets)
                break;
        }

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.Roar, xeno);

        args.Handled = true;

        var filter = Filter.Pvs(transform.Coordinates, entityMan: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
        foreach (var hit in actualResults)
        {
            var attackedEv = new AttackedEvent(xeno, xeno, args.Target);
            RaiseLocalEvent(hit, attackedEv);

            var change = _damage.TryChangeDamage(hit, xeno.Comp.Damage, true, false);

            if (change?.GetTotal() > FixedPoint2.Zero)
                _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { hit }, filter);

            if (_net.IsServer)
                SpawnAttachedTo(xeno.Comp.AttackEffect, hit.ToCoordinates());

            if (_empower.TryGetComponent(xeno, out var empower))
            {
                if (empower.EmpowerActive)
                {

                    if (_net.IsServer)
                        SpawnAttachedTo(xeno.Comp.EffectSlowdown, hit.ToCoordinates());

                    _stun.TrySlowdown(hit, xeno.Comp.Slowdown, false, 0f, 0f);
                }
            }
        }
        var localPos = transform.LocalRotation.RotateVec(matrix);
        var length = localPos.Length();
        localPos *= range / length;

        DoLunge((xeno, xeno, transform), localPos, "WeaponArcThrust");

        if (actualResults.Count > 0)
        {
            _rmcMelee.DoLunge(xeno, actualResults[0]);
            if (_net.IsServer)
                _audio.PlayPvs(xeno.Comp.Sound, xeno);
        }
        Dirty(xeno);
	}
    protected virtual void DoLunge(Entity<XenoScissorsCutComponent, TransformComponent> user, Vector2 localPos, EntProtoId animationId)
    {
    }
}
