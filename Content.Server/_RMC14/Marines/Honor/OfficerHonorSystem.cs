using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Honor;
using Content.Shared.Buckle;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Marines.Honor;

public sealed class OfficerHonorSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<OfficerHonorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OfficerHonorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OfficerHonorComponent, OfficerHonorActionEvent>(OnHonor);
    }

    private void OnMapInit(Entity<OfficerHonorComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        _actions.SetUseDelay(ent.Comp.ActionEntity, ent.Comp.Cooldown);
    }

    private void OnShutdown(Entity<OfficerHonorComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnHonor(Entity<OfficerHonorComponent> ent, ref OfficerHonorActionEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent))
            return;

        var now = Timing.CurTime;
        if (ent.Comp.NextHonorAt > now || !TryComp(ent, out TransformComponent? transform))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(transform.Coordinates, ent.Comp.Range, _nearby);

        EntityUid? seniorOfficer = null;
        var officerAuthority = GetAuthority(ent.Owner);
        foreach (var nearby in _nearby)
        {
            if (nearby != ent.Owner && GetAuthority(nearby) > officerAuthority)
            {
                seniorOfficer = nearby;
                break;
            }
        }

        var callout = seniorOfficer == null
            ? _random.Pick(new[] { "rmc-officer-honor-attention-1", "rmc-officer-honor-attention-2", "rmc-officer-honor-attention-3" })
            : "rmc-officer-honor-commander";
        _chat.TrySendInGameICMessage(ent, Loc.GetString(callout), InGameICChatType.Speak, false);

        var honorTarget = seniorOfficer ?? ent.Owner;
        var officerPosition = _transform.GetWorldPosition(honorTarget);
        if (honorTarget != ent.Owner && TryComp(honorTarget, out OfficerHonorComponent? honoredOfficer))
        {
            honoredOfficer.NextHonorAt = now + honoredOfficer.Cooldown;
            _actions.SetCooldown(honoredOfficer.ActionEntity, honoredOfficer.Cooldown);
        }

        foreach (var nearby in _nearby)
        {
            if (nearby == ent.Owner || nearby == honorTarget || !HasComp<MarineComponent>(nearby) || _mobState.IsDead(nearby))
                continue;

            _buckle.TryUnbuckle(nearby, nearby, popup: false);
            _standing.Stand(nearby);
            Face(nearby, officerPosition);
            _chat.TryEmoteWithChat(nearby, "Salute", forceEmote: true);

            if (TryComp(nearby, out OfficerHonorComponent? officer))
            {
                officer.NextHonorAt = now + officer.Cooldown;
                _actions.SetCooldown(officer.ActionEntity, officer.Cooldown);
            }
        }

        if (seniorOfficer != null)
        {
            Face(ent.Owner, officerPosition);
            _chat.TryEmoteWithChat(ent, "Salute", forceEmote: true);
        }

        ent.Comp.NextHonorAt = now + ent.Comp.Cooldown;
        _actions.SetCooldown(ent.Comp.ActionEntity, ent.Comp.Cooldown);
        args.Handled = true;
    }

    private void Face(EntityUid entity, System.Numerics.Vector2 targetPosition)
    {
        var direction = targetPosition - _transform.GetWorldPosition(entity);
        if (direction.LengthSquared() > 0)
            _transform.SetWorldRotation(entity, direction.ToWorldAngle());
    }

    private int GetAuthority(EntityUid entity)
    {
        return TryComp(entity, out MindContainerComponent? mind) && mind.Mind is { } mindId &&
               _jobs.MindTryGetJob(mindId, out var job)
            ? job.MarineAuthorityLevel
            : 0;
    }
}
