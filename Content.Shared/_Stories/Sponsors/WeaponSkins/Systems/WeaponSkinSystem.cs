using System.Linq;
using Content.Shared._RMC14.Item;
using Content.Shared._Stories.Sponsors.WeaponSkins.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Sponsors.WeaponSkins.Systems;

public sealed class WeaponSkinSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponSkinComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SprayPaintComponent, AfterInteractEvent>(OnSprayAfterInteract);
        SubscribeLocalEvent<WeaponSkinComponent, WeaponSkinAppliedEvent>(OnSkinAppliedDoAfter);
        SubscribeLocalEvent<SprayPaintComponent, GetVerbsEvent<InteractionVerb>>(AddSprayVerb);
    }

    private void OnMapInit(Entity<WeaponSkinComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsServer)
        {
            var skinToApply = ent.Comp.DefaultSkin;
            if (!ent.Comp.Skins.ContainsKey(skinToApply))
                skinToApply = ent.Comp.Skins.Keys.FirstOrDefault();

            if (skinToApply != null)
                _appearance.SetData(ent.Owner, WeaponSkinVisuals.Skin, skinToApply);
        }
    }

    private void AddSprayVerb(Entity<SprayPaintComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Using == null || args.Target == EntityUid.Invalid)
            return;

        if (args.Using != ent.Owner)
            return;

        if (!HasComp<WeaponSkinComponent>(args.Target))
            return;

        var sprayComp = ent.Comp;
        var target = args.Target;
        var user = args.User;

        InteractionVerb verb = new()
        {
            Text = Loc.GetString("stories-verb-spray-paint-apply", ("target", target)),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/paint.svg.192dpi.png")),
            Act = () => TryStartApplySkin(user, ent.Owner, target, sprayComp),
            Priority = 1
        };

        args.Verbs.Add(verb);
    }

    private void OnSprayAfterInteract(Entity<SprayPaintComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Used != ent.Owner)
            return;

        TryStartApplySkin(args.User, ent.Owner, args.Target.Value, ent.Comp);
    }

    private void TryStartApplySkin(EntityUid user, EntityUid sprayCanUid, EntityUid targetUid, SprayPaintComponent sprayComp)
    {
        if (!TryComp<WeaponSkinComponent>(targetUid, out var weaponSkinComp))
            return;

        if (!HasComp<GunComponent>(targetUid) && !HasComp<MeleeWeaponComponent>(targetUid))
        {
            _popup.PopupClient(Loc.GetString("stories-spray-paint-target-not-weapon", ("target", (targetUid, EntityManager))), user, user);
            return;
        }

        if (!weaponSkinComp.Skins.ContainsKey(sprayComp.SkinId))
        {
            _popup.PopupClient(Loc.GetString("stories-spray-paint-skin-not-supported", ("target", (targetUid, EntityManager)), ("skin", sprayComp.SkinId)), user, user);
            return;
        }

        if (_appearance.TryGetData<string>(targetUid, WeaponSkinVisuals.Skin, out var currentSkinId) && currentSkinId == sprayComp.SkinId)
        {
            if (currentSkinId != weaponSkinComp.DefaultSkin || !HasComp<ItemCamouflageComponent>(targetUid))
            {
                _popup.PopupClient(Loc.GetString("stories-spray-paint-skin-already-applied", ("target", (targetUid, EntityManager)), ("skin", sprayComp.SkinId)), user, user);
                return;
            }
        }

        _popup.PopupClient(Loc.GetString("stories-spray-paint-start", ("target", (targetUid, EntityManager))), user, user);

        var netUser = GetNetEntity(user);
        var netTarget = GetNetEntity(targetUid);
        var netSprayCan = GetNetEntity(sprayCanUid);

        var weaponSkinEvent = new WeaponSkinAppliedEvent(netUser, netTarget, netSprayCan, sprayComp.SkinId);

        var doAfterArgs = new DoAfterArgs(_entityManager, user, sprayComp.ApplyDuration, weaponSkinEvent, targetUid, target: targetUid, used: sprayCanUid)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnSkinAppliedDoAfter(Entity<WeaponSkinComponent> ent, ref WeaponSkinAppliedEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryGetEntity(args.TargetEntity, out var targetUid) ||
            !TryGetEntity(args.SprayCanEntity, out var sprayCanUid) ||
            !TryGetEntity(args.ApplyingUser, out var userUid))
        {
            return;
        }

        if (!TryComp<WeaponSkinComponent>(targetUid, out var skinComp) ||
            !TryComp<SprayPaintComponent>(sprayCanUid.Value, out var sprayComp))
            return;

        if (!skinComp.Skins.ContainsKey(args.SkinId))
            return;

        _appearance.SetData(targetUid.Value, WeaponSkinVisuals.Skin, args.SkinId);
        _popup.PopupClient(Loc.GetString("stories-spray-paint-finish", ("target", (targetUid.Value, EntityManager))), userUid.Value);

        if (!_net.IsServer)
            return;

        RemComp<ItemCamouflageComponent>(targetUid.Value);


        if (sprayComp.Uses != null)
        {
            sprayComp.Uses--;
            Dirty(sprayCanUid.Value, sprayComp);
            if (sprayComp.Uses <= 0)
            {
                _entityManager.QueueDeleteEntity(sprayCanUid.Value);
            }
        }
    }
}
