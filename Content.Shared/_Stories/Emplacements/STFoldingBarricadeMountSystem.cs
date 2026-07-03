using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Emplacements;

public sealed class StoriesFoldingBarricadeMountSystem : EntitySystem
{
    private const string MagazineSlotId = "gun_magazine";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(RMCFoldingBarricadeSystem) });
        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, StoriesFoldingBarricadeMountInstallDoAfterEvent>(OnInstallDoAfter);
        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, StoriesFoldingBarricadeMountUninstallDoAfterEvent>(OnUninstallDoAfter);
        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, RMCFoldingBarricadeCollapseDoAfterEvent>(OnCollapseDoAfter,
            before: new[] { typeof(RMCFoldingBarricadeSystem) });
        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StoriesFoldingBarricadeMountableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs,
            after: new[] { typeof(RMCFoldingBarricadeSystem) });
    }

    private void OnInteractUsing(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<MountableWeaponComponent>(args.Used))
        {
            if (HasComp<WeaponMountComponent>(ent.Owner) || ent.Comp.TargetPrototype is not { } target)
                return;

            if (ent.Comp.WeaponWhitelist != null && !_whitelist.IsWhitelistPass(ent.Comp.WeaponWhitelist, args.Used))
                return;

            if (!_skills.HasSkill(args.User, ent.Comp.EngineerSkill, ent.Comp.RequiredSkillLevel))
            {
                _popup.PopupClient(Loc.GetString("stories-folding-barricade-mount-untrained"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            args.Handled = true;

            PopupPredicted(
                Loc.GetString("stories-folding-barricade-mount-install-start", ("barricade", ent)),
                Loc.GetString("stories-folding-barricade-mount-install-start-others", ("user", args.User), ("barricade", ent)),
                args.User);

            StartDoAfter(ent, args.User, args.Used, new StoriesFoldingBarricadeMountInstallDoAfterEvent());
            return;
        }

        if (!TryComp(ent.Owner, out WeaponMountComponent? weaponMount) || weaponMount.MountedEntity == null)
            return;

        if (ent.Comp.RevertPrototype != null && _tool.HasQuality(args.Used, weaponMount.DismantlingTool))
        {
            if (!_skills.HasSkill(args.User, ent.Comp.EngineerSkill, ent.Comp.RequiredSkillLevel))
            {
                _popup.PopupClient(Loc.GetString("stories-folding-barricade-mount-untrained"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            args.Handled = true;

            PopupPredicted(
                Loc.GetString("stories-folding-barricade-mount-uninstall-start", ("barricade", ent)),
                Loc.GetString("stories-folding-barricade-mount-uninstall-start-others", ("user", args.User), ("barricade", ent)),
                args.User);

            StartDoAfter(ent, args.User, args.Used, new StoriesFoldingBarricadeMountUninstallDoAfterEvent());
            return;
        }

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-folding-barricade-mount-collapse-blocked"), ent, args.User, PopupType.SmallCaution);
    }

    private void PopupPredicted(string self, string others, EntityUid user)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _popup.PopupPredicted(self, others, user, user);
    }

    private void StartDoAfter(Entity<StoriesFoldingBarricadeMountableComponent> ent, EntityUid user, EntityUid used, DoAfterEvent ev)
    {
        var skill = _skills.GetSkill(user, ent.Comp.EngineerSkill);
        var range = ent.Comp.MaxSkillLevel - ent.Comp.RequiredSkillLevel;
        var t = range > 0
            ? Math.Clamp((skill - ent.Comp.RequiredSkillLevel) / (float) range, 0f, 1f)
            : 1f;

        var maxSeconds = ent.Comp.MaxDelay.TotalSeconds;
        var minSeconds = ent.Comp.MinDelay.TotalSeconds;
        var delay = TimeSpan.FromSeconds(maxSeconds + (minSeconds - maxSeconds) * t);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, ev, ent, ent, used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnInstallDoAfter(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref StoriesFoldingBarricadeMountInstallDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } weapon || ent.Comp.TargetPrototype is not { } target)
            return;

        args.Handled = true;

        if (!_net.IsServer)
            return;

        var xform = Transform(ent);
        var coordinates = xform.Coordinates;
        var rotation = xform.LocalRotation;

        var damage = 0f;
        if (TryComp(ent.Owner, out DamageableComponent? damageable))
            damage = damageable.TotalDamage.Float();

        var mount = SpawnAtPosition(target, coordinates);
        var mountXform = Transform(mount);
        _transform.SetLocalRotation(mount, rotation, mountXform);

        if (!mountXform.Anchored)
            _transform.AnchorEntity((mount, mountXform));

        ApplyStoredDamage(mount, damage);
        TransferMagazine(weapon, mount);

        QueueDel(ent.Owner);
        QueueDel(weapon);

        _popup.PopupEntity(
            Loc.GetString("stories-folding-barricade-mount-install-finish", ("barricade", mount)),
            mount,
            args.User);
        _audio.PlayPvs(ent.Comp.InstallSound, mount);
    }

    private void OnUninstallDoAfter(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref StoriesFoldingBarricadeMountUninstallDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.RevertPrototype is not { } revert)
            return;

        args.Handled = true;

        if (!_net.IsServer)
            return;

        if (!TryComp(ent.Owner, out WeaponMountComponent? weaponMount) || weaponMount.MountedEntity is not { } gun)
            return;

        if (_container.TryGetContainer(ent.Owner, weaponMount.WeaponSlotId, out var container))
            _container.Remove(gun, container);

        var xform = Transform(ent);
        var coordinates = xform.Coordinates;
        var rotation = xform.LocalRotation;

        var damage = 0f;
        if (TryComp(ent.Owner, out DamageableComponent? damageable))
            damage = damageable.TotalDamage.Float();

        var barricade = SpawnAtPosition(revert, coordinates);
        var barricadeXform = Transform(barricade);
        _transform.SetLocalRotation(barricade, rotation, barricadeXform);

        if (!barricadeXform.Anchored)
            _transform.AnchorEntity((barricade, barricadeXform));

        ApplyStoredDamage(barricade, damage);

        QueueDel(ent.Owner);

        if (TryComp(args.User, out HandsComponent? hands))
            _hands.TryPickupAnyHand(args.User, gun, handsComp: hands);

        _popup.PopupEntity(
            Loc.GetString("stories-folding-barricade-mount-uninstall-finish", ("barricade", barricade)),
            barricade,
            args.User);
        _audio.PlayPvs(ent.Comp.UninstallSound, barricade);
    }

    private void OnCollapseDoAfter(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref RMCFoldingBarricadeCollapseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !IsArmed(ent.Owner))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-folding-barricade-mount-collapse-blocked"), ent, args.User, PopupType.SmallCaution);
    }

    private void OnExamined(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<WeaponMountComponent>(ent.Owner) || ent.Comp.TargetPrototype == null)
            return;

        using (args.PushGroup(nameof(StoriesFoldingBarricadeMountableComponent)))
        {
            args.PushMarkup(Loc.GetString("stories-folding-barricade-mount-examine-installable"));
        }
    }

    private void OnGetAlternativeVerbs(Entity<StoriesFoldingBarricadeMountableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsArmed(ent.Owner))
            return;

        var collapseText = Loc.GetString("rmc-folding-barricade-collapse-verb");
        args.Verbs.RemoveWhere(verb => verb.Text == collapseText);
    }

    private bool IsArmed(EntityUid barricade)
    {
        return TryComp(barricade, out WeaponMountComponent? weaponMount) && weaponMount.MountedEntity != null;
    }

    private void ApplyStoredDamage(EntityUid target, float damage)
    {
        if (damage <= 0 || !TryComp(target, out DamageableComponent? damageable))
            return;

        var spec = _rmcDamageable.DistributeTypesTotal((target, damageable), FixedPoint2.New(damage));
        _damageable.TryChangeDamage(target, spec, true, false, damageable);
    }

    private void TransferMagazine(EntityUid weapon, EntityUid mount)
    {
        if (!TryComp(mount, out WeaponMountComponent? weaponMount) || weaponMount.MountedEntity is not { } mountedGun)
            return;

        if (!_slots.TryGetSlot(weapon, MagazineSlotId, out var sourceSlot) || sourceSlot.Item == null)
            return;

        if (!_slots.TryGetSlot(mountedGun, MagazineSlotId, out var targetSlot))
            return;

        if (!_slots.TryEject(weapon, sourceSlot, null, out var magazine, true))
            return;

        _slots.TryInsert(mountedGun, targetSlot, magazine.Value, null, true);
    }
}

[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class StoriesFoldingBarricadeMountInstallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class StoriesFoldingBarricadeMountUninstallDoAfterEvent : SimpleDoAfterEvent;
