using System.Linq;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.Hunter.Teleporter.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Hunter.Teleporter;

public sealed class HunterTeleporterSystem : EntitySystem
{
    private const string IllegalTechnologySkillId = "STSkillIllegalTechnology";

    private const float InteractionRange = 1f;
    private static readonly ProtoId<NpcFactionPrototype> HunterFaction = "STHunter";
    private static readonly ProtoId<NpcFactionPrototype> HunterYoungFaction = "STHunterYoung";
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterTeleporterComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<HunterTeleporterUserComponent, DialogChosenEvent>(OnDialogChosen);
    }

    private void OnStartCollide(Entity<HunterTeleporterComponent> ent, ref StartCollideEvent args)
    {
        var user = args.OtherEntity;

        if (
            HasComp<HunterTeleporterUserComponent>(user)
            && Comp<HunterTeleporterUserComponent>(user).NextUse > _timing.CurTime
        )
            return;

        if (!_net.IsServer)
            return;

        var userComp = EnsureComp<HunterTeleporterUserComponent>(user);
        userComp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(2);
        userComp.InteractingWith = ent;

        if (!_skills.HasSkill(user, new EntProtoId<SkillDefinitionComponent>(IllegalTechnologySkillId), 1))
        {
            _popup.PopupClient(Loc.GetString("st-hunter-teleporter-no-tech"), user, user);
            return;
        }

        if (TryComp<NpcFactionMemberComponent>(user, out var factionComp))
        {
            var factions = factionComp.Factions;

            if (ent.Comp.TeleporterType == HunterTeleporterType.Normal && factions.Contains(HunterYoungFaction))
            {
                _popup.PopupClient(Loc.GetString("st-hunter-teleporter-young-no-access"), user, user);
                return;
            }

            if (ent.Comp.TeleporterType == HunterTeleporterType.Youngblood && !factions.Contains(HunterYoungFaction))
            {
                _popup.PopupClient(Loc.GetString("st-hunter-teleporter-elder-no-access"), user, user);
                return;
            }

            if (!factions.Contains(HunterFaction) && !factions.Contains(HunterYoungFaction))
                return;
        }
        else
            return;

        var destinations = new List<(EntityUid Uid, string Name)>();
        var query = EntityQueryEnumerator<HunterTeleportDestinationComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var destComp, out var destTransform))
        {
            if (!string.IsNullOrEmpty(destComp.DestinationGroup))
                continue;

            if (destComp.TeleporterType != ent.Comp.TeleporterType)
                continue;

            var locationName = "Unknown Area";
            if (_area.TryGetArea(uid, out _, out var areaProto))
                locationName = areaProto.Name;

            destinations.Add((uid, locationName));
        }

        if (destinations.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("st-hunter-teleporter-no-destinations"), user, user);
            return;
        }

        destinations = destinations.OrderBy(d => d.Name).ToList();

        var options = new List<DialogOption>();
        ent.Comp.ActiveDestinations.Clear();

        for (var i = 0; i < destinations.Count; i++)
        {
            var (uid, name) = destinations[i];
            options.Add(new DialogOption(name));
            ent.Comp.ActiveDestinations[i] = uid;
        }

        _dialog.OpenOptions(user, user, Loc.GetString("st-hunter-teleporter-title"), options);
    }

    private void OnDialogChosen(Entity<HunterTeleporterUserComponent> user, ref DialogChosenEvent args)
    {
        if (!_net.IsServer)
            return;

        var teleporterUid = user.Comp.InteractingWith;

        if (!TryComp<HunterTeleporterComponent>(teleporterUid, out var teleporterComp))
        {
            RemComp<HunterTeleporterUserComponent>(user);
            return;
        }

        if (!_transform.InRange(user.Owner, teleporterUid.Value, InteractionRange))
        {
            _popup.PopupClient(Loc.GetString("st-hunter-teleporter-too-far"), user, user);
            RemComp<HunterTeleporterUserComponent>(user);
            return;
        }

        if (!teleporterComp.ActiveDestinations.TryGetValue(args.Index, out var destinationUid))
        {
            RemComp<HunterTeleporterUserComponent>(user);
            return;
        }

        teleporterComp.ActiveDestinations.Clear();

        if (!Exists(destinationUid))
        {
            RemComp<HunterTeleporterUserComponent>(user);
            return;
        }

        var destinationCoords = _transform.GetMoverCoordinates(destinationUid);
        _transform.SetCoordinates(args.Actor, destinationCoords);
        _audio.PlayPvs(teleporterComp.TeleportSound, args.Actor);

        RemComp<HunterTeleporterUserComponent>(user);
    }
}
