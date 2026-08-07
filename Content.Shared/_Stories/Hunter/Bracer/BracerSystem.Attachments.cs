using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializeAttachments()
    {
        SubscribeLocalEvent<HunterBracerComponent, ToggleBracerAttachmentsEvent>(OnToggleAttachments);

        SubscribeLocalEvent<BracerAttachedWeaponComponent, DroppedEvent>(OnAttachedWeaponDropped);
        SubscribeLocalEvent<BracerAttachedWeaponComponent, AfterInteractEvent>(OnAttachedWeaponAfterInteract);

        SubscribeLocalEvent<BracerAttachedWeaponComponent, BracerPryDoAfterEvent>(OnPryDoAfter);
        SubscribeLocalEvent<BracerAttachedWeaponComponent, BracerSmashPryDoAfterEvent>(OnSmashPryDoAfter);

        SubscribeLocalEvent<BracerAttachmentComponent, ItemSlotEjectAttemptEvent>(OnAttachmentEjectAttempt);
    }

    private void OnAttachmentEjectAttempt(Entity<BracerAttachmentComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.AttachedWeapon.HasValue)
        {
            args.Cancelled = true;
        }
    }

    private void OnToggleAttachments(Entity<HunterBracerComponent> ent, ref ToggleBracerAttachmentsEvent args)
    {
        if (args.Handled)
            return;

        var hasAttachment =
            _itemSlots.TryGetSlot(ent, LeftAttachmentSlotId, out var left) && left.Item is not null
            || _itemSlots.TryGetSlot(ent, RightAttachmentSlotId, out var right) && right.Item is not null;

        if (!hasAttachment)
        {
            _popup.PopupClient(Loc.GetString("st-bracer-no-attachments"), args.Performer, args.Performer);
            return;
        }

        if (!AttemptUsage(args.Performer, ent))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        var isDeployed =
            IsAttachmentDeployed(ent, LeftAttachmentSlotId, args.Performer)
            || IsAttachmentDeployed(ent, RightAttachmentSlotId, args.Performer);

        if (isDeployed)
            RetractAttachments(args.Performer, ent);
        else
            DeployAttachments(args.Performer, ent);
    }

    private void DeployAttachments(EntityUid user, Entity<HunterBracerComponent> bracer)
    {
        if (!TryDrainPower(user, bracer, bracer.Comp.AttachmentDeployCost))
            return;

        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        var leftHandId = hands.Hands.Keys.FirstOrDefault(id => id.ToLower().Contains("left"));
        var rightHandId = hands.Hands.Keys.FirstOrDefault(id => id.ToLower().Contains("right"));

        var playedSound = false;

        if (leftHandId != null)
        {
            if (TryDeployAttachment(user, bracer, LeftAttachmentSlotId, leftHandId, hands, out _))
                playedSound = true;
        }

        if (rightHandId != null)
        {
            if (TryDeployAttachment(user, bracer, RightAttachmentSlotId, rightHandId, hands, out _))
                playedSound = true;
        }

        if (playedSound)
        {
            _popup.PopupEntity(Loc.GetString("st-bracer-attachments-deployed"), user, user);

            if (bracer.Comp.ToggleAttachmentsAction.HasValue)
                _actions.SetToggled(bracer.Comp.ToggleAttachmentsAction.Value, true);
        }
    }

    private bool TryDeployAttachment(
        EntityUid user,
        Entity<HunterBracerComponent> bracer,
        string slotId,
        string handId,
        HandsComponent hands,
        [NotNullWhen(true)] out EntityUid? deployedWeapon
    )
    {
        deployedWeapon = null;
        if (_hands.TryGetHeldItem((user, hands), handId, out _))
            return false;

        if (!_itemSlots.TryGetSlot(bracer.Owner, slotId, out var itemSlot) || itemSlot.Item is not { } attachmentUid)
            return false;

        if (!_itemSlots.TryGetSlot(attachmentUid, AttachmentWeaponSlotId, out var weaponSlot))
            return false;

        if (!TryComp<BracerAttachmentComponent>(attachmentUid, out var attachmentComp))
            return false;

        var wasLocked = weaponSlot.Locked;
        _itemSlots.SetLock(attachmentUid, weaponSlot, false);

        try
        {
            if (_itemSlots.TryEject(attachmentUid, weaponSlot, user, out var ejectedWeapon, true))
            {
                if (_hands.TryPickup(user, ejectedWeapon.Value, handId, false, handsComp: hands))
                {
                    _audio.PlayPvs(attachmentComp.DeploySound, user);
                    EnsureComp<UnremoveableComponent>(ejectedWeapon.Value);
                    attachmentComp.AttachedWeapon = ejectedWeapon.Value;
                    Dirty(attachmentUid, attachmentComp);
                    deployedWeapon = ejectedWeapon.Value;
                    return true;
                }
                else
                    _itemSlots.TryInsert(attachmentUid, weaponSlot, ejectedWeapon.Value, null, true);
            }
        }
        finally
        {
            _itemSlots.SetLock(attachmentUid, weaponSlot, wasLocked);
        }

        return false;
    }

    private void RetractAttachments(EntityUid user, Entity<HunterBracerComponent> bracer, bool quiet = false)
    {
        var retracted = false;
        if (TryRetractAttachment(user, bracer, LeftAttachmentSlotId, out _, quiet))
            retracted = true;

        if (TryRetractAttachment(user, bracer, RightAttachmentSlotId, out _, quiet))
            retracted = true;

        if (retracted)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("st-bracer-attachments-retracted"), user, user);

            if (bracer.Comp.ToggleAttachmentsAction.HasValue)
                _actions.SetToggled(bracer.Comp.ToggleAttachmentsAction.Value, false);
        }
    }

    private bool TryRetractAttachment(
        EntityUid user,
        Entity<HunterBracerComponent> bracer,
        string slotId,
        [NotNullWhen(true)] out EntityUid? retractedWeapon,
        bool quiet
    )
    {
        retractedWeapon = null;
        if (!_itemSlots.TryGetSlot(bracer.Owner, slotId, out var itemSlot) || itemSlot.Item is not { } attachmentUid)
            return false;

        if (!_itemSlots.TryGetSlot(attachmentUid, AttachmentWeaponSlotId, out var weaponSlot))
            return false;

        if (
            !TryComp<BracerAttachmentComponent>(attachmentUid, out var attachmentComp)
            || !attachmentComp.AttachedWeapon.HasValue
        )
            return false;

        var weapon = attachmentComp.AttachedWeapon.Value;

        if (_hands.IsHolding(user, weapon, out _))
        {
            RemComp<UnremoveableComponent>(weapon);

            var wasLocked = weaponSlot.Locked;
            _itemSlots.SetLock(attachmentUid, weaponSlot, false);
            try
            {
                if (_itemSlots.TryInsert(attachmentUid, weaponSlot, weapon, user, true))
                {
                    if (!quiet)
                        _audio.PlayPvs(attachmentComp.RetractSound, user);

                    attachmentComp.AttachedWeapon = null;
                    Dirty(attachmentUid, attachmentComp);
                    retractedWeapon = weapon;
                    return true;
                }
            }
            finally
            {
                _itemSlots.SetLock(attachmentUid, weaponSlot, wasLocked);
            }

            EnsureComp<UnremoveableComponent>(weapon);
        }

        return false;
    }

    private void OnAttachedWeaponDropped(Entity<BracerAttachedWeaponComponent> ent, ref DroppedEvent args)
    {
        var bracerUid = GetEntity(ent.Comp.Bracer);
        if (TryComp<HunterBracerComponent>(bracerUid, out var bracerComp))
            RetractAttachments(args.User, (bracerUid.Value, bracerComp));
    }

    private bool IsAttachmentDeployed(Entity<HunterBracerComponent> bracer, string slotId, EntityUid user)
    {
        if (!user.IsValid())
            return false;

        if (!_itemSlots.TryGetSlot(bracer.Owner, slotId, out var itemSlot) || itemSlot.Item is not { } attachmentUid)
            return false;

        if (
            !TryComp<BracerAttachmentComponent>(attachmentUid, out var attachmentComp)
            || !attachmentComp.AttachedWeapon.HasValue
        )
            return false;

        return _hands.IsHolding(user, attachmentComp.AttachedWeapon.Value, out _);
    }

    private void OnAttachedWeaponAfterInteract(Entity<BracerAttachedWeaponComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_net.IsClient)
            return;

        if (TryComp<DoorComponent>(target, out var door))
        {
            if (door.State != DoorState.Closed || _door.IsBolted(target) || door.State == DoorState.Welded)
                return;

            args.Handled = true;
            _audio.PlayPvs(
                new SoundPathSpecifier("/Audio/_Stories/Weapons/wristblades_hit.ogg"),
                args.User
            );

            if (HasComp<BracerSmashPryComponent>(ent))
            {
                _popup.PopupEntity(Loc.GetString("st-bracer-smash-pry-door-start"), args.User, args.User);
                var doAfter = new DoAfterArgs(
                    EntityManager,
                    args.User,
                    TimeSpan.FromSeconds(3),
                    new BracerSmashPryDoAfterEvent(),
                    ent.Owner,
                    target,
                    ent.Owner
                )
                {
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnDamage = true,
                };
                _doAfter.TryStartDoAfter(doAfter);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("st-bracer-pry-door-start"), args.User, args.User);
                var doAfter = new DoAfterArgs(
                    EntityManager,
                    args.User,
                    TimeSpan.FromSeconds(3),
                    new BracerPryDoAfterEvent(),
                    ent.Owner,
                    target,
                    ent.Owner
                )
                {
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnDamage = true,
                };
                _doAfter.TryStartDoAfter(doAfter);
            }
        }
    }

    private void OnPryDoAfter(Entity<BracerAttachedWeaponComponent> ent, ref BracerPryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target || !_net.IsServer)
            return;

        if (!TryComp<DoorComponent>(target, out var door))
            return;

        if (door.State != DoorState.Closed || _door.IsBolted(target) || door.State == DoorState.Welded)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("st-bracer-pry-door-success"), args.User, args.User);
        _door.TryOpen(target, door, args.User, true);
    }

    private void OnSmashPryDoAfter(Entity<BracerAttachedWeaponComponent> ent, ref BracerSmashPryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target || !_net.IsServer)
            return;

        if (!TryComp<DoorComponent>(target, out var door))
            return;

        if (door.State != DoorState.Closed || _door.IsBolted(target) || door.State == DoorState.Welded)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("st-bracer-smash-pry-door-success"), args.User, args.User);

        if (TryComp<BracerSmashPryComponent>(ent, out var smashComp))
            _audio.PlayPvs(smashComp.SmashSound, ent);

        _damageable.TryChangeDamage(
            target,
            new DamageSpecifier(_prototype.Index<DamageTypePrototype>("Brute"), 200),
            true,
            origin: args.User
        );
    }
}
