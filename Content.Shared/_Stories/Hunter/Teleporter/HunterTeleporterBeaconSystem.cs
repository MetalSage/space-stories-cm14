using System.Linq;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._Stories.Hunter.Teleporter.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Hunter.Teleporter;

public sealed partial class HunterTeleporterBeaconSystem : EntitySystem
{
    private const string IllegalTechnologySkillId = "STSkillIllegalTechnology";
    private static readonly ProtoId<NpcFactionPrototype> HunterFaction = "STHunter";
    private static readonly ProtoId<NpcFactionPrototype> HunterYoungFaction = "STHunterYoung";
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterTeleporterBeaconComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HunterTeleporterBeaconComponent, TeleporterDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<HunterTeleporterBeaconComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<BeaconUserComponent, HunterBeaconDestinationChosenEvent>(OnBeaconDestinationChosen);
    }

    private void OnUseInHand(Entity<HunterTeleporterBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_net.IsServer)
            return;

        var user = args.User;

        if (ent.Comp.NextUse > _timing.CurTime)
        {
            var timeLeft = ent.Comp.NextUse - _timing.CurTime;
            _popup.PopupClient(
                Loc.GetString(
                    "st-hunter-teleporter-beacon-on-cooldown",
                    ("seconds", timeLeft.TotalSeconds.ToString("0"))
                ),
                user,
                user
            );
            return;
        }

        if (!_skills.HasSkill(user, new EntProtoId<SkillDefinitionComponent>(IllegalTechnologySkillId), 1))
        {
            _popup.PopupClient(Loc.GetString("st-hunter-teleporter-beacon-no-skill"), user, user);
            return;
        }

        if (TryComp<NpcFactionMemberComponent>(user, out var factionComp))
        {
            var factions = factionComp.Factions;
            if (factions.Contains(HunterYoungFaction))
            {
                _popup.PopupClient(Loc.GetString("st-hunter-teleporter-beacon-youngblood"), user, user);
                return;
            }
        }

        var destinationGroups = new SortedSet<string>();
        var query = EntityQueryEnumerator<HunterTeleportDestinationComponent>();
        while (query.MoveNext(out _, out var dest))
        {
            if (string.IsNullOrEmpty(dest.DestinationGroup))
                continue;

            destinationGroups.Add(dest.DestinationGroup);
        }

        var options = destinationGroups
            .Select(group => new DialogOption(group, new HunterBeaconDestinationChosenEvent(group)))
            .ToList();

        var userComp = EnsureComp<BeaconUserComponent>(user);
        userComp.Beacon = ent;

        userComp.AvailableGroups = null; 

        _dialog.OpenOptions(user, user, Loc.GetString("st-hunter-teleporter-beacon-title"), options);
        args.Handled = true;
    }

    private void OnBeaconDestinationChosen(Entity<BeaconUserComponent> user, ref HunterBeaconDestinationChosenEvent args)
    {
        if (!TryComp<HunterTeleporterBeaconComponent>(user.Comp.Beacon, out var beaconComp))
        {
            RemComp<BeaconUserComponent>(user);
            return;
        }

        user.Comp.TargetGroup = args.Group;

        _popup.PopupClient(Loc.GetString("st-hunter-teleporter-beacon-activating"), user, user);
        _audio.PlayPvs(beaconComp.ActivateSound, user);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            beaconComp.DoAfterDuration,
            new TeleporterDoAfterEvent(),
            user.Comp.Beacon,
            user.Comp.Beacon
        )
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<HunterTeleporterBeaconComponent> ent, ref TeleporterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.User == null)
            return;

        if (!TryComp<BeaconUserComponent>(args.User, out var userComp) || userComp.TargetGroup == null)
            return;

        var targetGroup = userComp.TargetGroup;

        var destinations = new List<EntityUid>();
        var query = EntityQueryEnumerator<HunterTeleportDestinationComponent>();
        while (query.MoveNext(out var uid, out var dest))
        {
            if (dest.DestinationGroup == targetGroup)
                destinations.Add(uid);
        }

        if (!destinations.Any())
        {
            _popup.PopupClient(
                Loc.GetString("st-hunter-teleporter-beacon-no-destinations-in-group", ("group", targetGroup)),
                args.User,
                args.User
            );
            return;
        }

        var randomDestination = _random.Pick(destinations);
        var targetCoords = Transform(randomDestination).Coordinates;

        _popup.PopupEntity(Loc.GetString("st-hunter-teleporter-beacon-disappears"), args.User, args.User);
        _transform.SetCoordinates(args.User, targetCoords);

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);

        args.Handled = true;
        RemComp<BeaconUserComponent>(args.User);
    }

    private void OnExamine(Entity<HunterTeleporterBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.NextUse > _timing.CurTime)
        {
            var timeLeft = ent.Comp.NextUse - _timing.CurTime;
            var msg = FormattedMessage.FromMarkup(
                Loc.GetString(
                    "st-hunter-teleporter-beacon-examine-cooldown",
                    ("seconds", timeLeft.TotalSeconds.ToString("0"))
                )
            );
            args.PushMessage(msg);
        }
    }

    [RegisterComponent]
    private sealed partial class BeaconUserComponent : Component
    {
        public List<string>? AvailableGroups;
        public EntityUid? Beacon;
        public string? TargetGroup;
    }

    [Serializable] [NetSerializable]
    private sealed partial class TeleporterDoAfterEvent : SimpleDoAfterEvent
    {
    }

    [Serializable] [NetSerializable]
    private sealed class HunterBeaconDestinationChosenEvent : EntityEventArgs
    {
        public string Group;

        public HunterBeaconDestinationChosenEvent(string group)
        {
            Group = group;
        }
    }
}
