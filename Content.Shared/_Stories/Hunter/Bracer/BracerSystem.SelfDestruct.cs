using Content.Shared._RMC14.Rules;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private static readonly ProtoId<NpcFactionPrototype> HunterYoungFaction = "STHunterYoung";

    private void InitializeSelfDestruct()
    {
        SubscribeLocalEvent<HunterBracerComponent, RequestSelfDestructEvent>(OnRequestSelfDestruct);
        SubscribeLocalEvent<BracerSelfDestructingComponent, ComponentRemove>(OnSelfDestructComponentRemove);
    }

    private void OnRequestSelfDestruct(Entity<HunterBracerComponent> ent, ref RequestSelfDestructEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.Performer;

        if (_net.IsClient && !IsAuthorized(user, ent.Comp))
            return;

        if (HasComp<BracerSelfDestructingComponent>(ent))
        {
            if (_mobState.IsIncapacitated(user))
            {
                _popup.PopupClient(Loc.GetString("st-bracer-sd-cancel-incapacitated"), user, user);
                return;
            }

            if (!AttemptUsage(user, ent, true))
                return;

            if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("st-bracer-sd-cancelled"), user, user);

                RemComp<BracerSelfDestructingComponent>(ent);
                _adminLog.Add(
                    LogType.Action,
                    LogImpact.Medium,
                    $"{ToPrettyString(user)} cancelled their self-destruct sequence."
                );
                if (ent.Comp.SelfDestructAction is { } action)
                    _actions.SetToggled(action, false);
            }
        }
        else
        {
            if (_mobState.IsIncapacitated(user))
            {
                _popup.PopupClient(Loc.GetString("st-bracer-sd-incapacitated"), user, user);
                return;
            }

            if (HasComp<BracerCloakedComponent>(user))
            {
                _popup.PopupClient(Loc.GetString("st-bracer-sd-cloaked"), user, user);
                return;
            }

            if (_npcFaction.IsMember(user, HunterYoungFaction))
            {
                _popup.PopupClient(Loc.GetString("st-bracer-sd-youngblood"), user, user);
                return;
            }

            var mapUid = Transform(user).MapUid;
            if (mapUid.HasValue && !HasComp<RMCPlanetComponent>(mapUid.Value))
            {
                _popup.PopupClient(Loc.GetString("st-bracer-sd-ship-denied"), user, user);
                return;
            }

            if (!AttemptUsage(user, ent, true))
                return;

            if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("st-bracer-sd-activated"), user, user);

                _adminLog.Add(
                    LogType.Action,
                    LogImpact.High,
                    $"{ToPrettyString(user)} activated their self-destruct sequence."
                );

                var sdComp = AddComp<BracerSelfDestructingComponent>(ent);
                sdComp.ExplosionTime = _timing.CurTime + ent.Comp.CountdownDuration;

                ent.Comp.Locked = true;
                Dirty(ent);

                if (ent.Comp.ExplosionType == SelfDestructType.Big)
                    _audio.PlayPvs(ent.Comp.DeathlaughSound, ent);

                var stream = _audio.PlayPvs(ent.Comp.CountdownSound, ent);
                sdComp.CountdownSoundStream = stream?.Entity;

                if (ent.Comp.SelfDestructAction is { } action)
                    _actions.SetToggled(action, true);
            }
        }
    }

    private void OnSelfDestructComponentRemove(Entity<BracerSelfDestructingComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;
        _audio.Stop(ent.Comp.CountdownSoundStream);
    }

    private void UpdateSelfDestruct()
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<BracerSelfDestructingComponent, HunterBracerComponent>();
        while (query.MoveNext(out var uid, out var sdComp, out var bracer))
        {
            if (_timing.CurTime < sdComp.ExplosionTime)
                continue;

            Explode(uid, bracer);
            RemComp<BracerSelfDestructingComponent>(uid);
        }
    }

    private void Explode(EntityUid bracerUid, HunterBracerComponent bracer)
    {
        var xform = Transform(bracerUid);
        var coords = xform.Coordinates;
        var wearer = xform.ParentUid;
        var mapUid = xform.MapUid;

        if (wearer.IsValid() && HasComp<HandsComponent>(wearer))
        {
            _adminLog.Add(
                LogType.Explosion,
                LogImpact.Extreme,
                $"{ToPrettyString(wearer)} has self-destructed via bracer {ToPrettyString(bracerUid)}."
            );
            _transform.DetachEntity(bracerUid, xform);
            QueueDel(wearer);
        }
        else
        {
            _adminLog.Add(
                LogType.Explosion,
                LogImpact.Extreme,
                $"Bracer {ToPrettyString(bracerUid)} has self-destructed while not equipped."
            );
        }

        var protoId =
            bracer.ExplosionType == SelfDestructType.Big
            && mapUid.HasValue
            && HasComp<RMCPlanetComponent>(mapUid.Value)
                ? bracer.BigExplosionId
                : bracer.SmallExplosionId;

        Spawn(protoId, coords);
    }
}
