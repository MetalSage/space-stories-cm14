using Content.Shared._RMC14.Evasion;
using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Sound;
using Content.Shared._Stories.LeadershipWhistle;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Sound.Components;
using Content.Shared.Timing;
using Content.Shared.Whistle;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Whistle;

public sealed class RMCWhistleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly WhistleSystem _whistle = default!;

    // Space Stories - Whistles - Start
    [Dependency] private readonly STWhistleSystem _stWhistle = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly EvasionSystem _evasion = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    // Space Stories - Whistles - End

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCWhistleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCWhistleComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<RMCWhistleComponent, SoundActionEvent>(OnWhistleAction);

        // Space Stories - Whistles - Start
        SubscribeLocalEvent<MoveActionEvent>(OnOrderAction);
        SubscribeLocalEvent<HoldActionEvent>(OnOrderAction);
        SubscribeLocalEvent<FocusActionEvent>(OnOrderAction);
        // Space Stories - Whistles - End
    }

    #region ST-Whistles

    private void OnOrderAction(MoveActionEvent ev)
    {
        OnOrderAction<MoveActionEvent>(ev.Performer, STOrderTypes.Move);
    }

    private void OnOrderAction(HoldActionEvent ev)
    {
        OnOrderAction<HoldActionEvent>(ev.Performer, STOrderTypes.Hold);
    }

    private void OnOrderAction(FocusActionEvent ev)
    {
        OnOrderAction<FocusActionEvent>(ev.Performer, STOrderTypes.Focus);
    }

    private void OnOrderAction<T>(EntityUid user, STOrderTypes order) where T : InstantActionEvent
    {
        if (!_stWhistle.TryGetWhistleInSlotOrHands(user, out var whistle) || whistle is not { })
            return;
        if (!TryComp<EmitSoundOnUseComponent>(whistle, out var emitOnUseComp))
            return;

        if (TryComp<LeadershipWhistleComponent>(whistle.Value, out var leaderWhistleComp))
        {
            _audio.PlayPredicted(leaderWhistleComp.OrderWhistleSounds[order], user, user);
            switch (order)
            {
                case STOrderTypes.Move:
                    var moveOrder = Comp<MoveOrderComponent>(user);
                    moveOrder.MoveSpeedModifier *= leaderWhistleComp.MultiplierOrderBuff;
                    moveOrder.EvasionModifier *= leaderWhistleComp.MultiplierOrderBuff;

                    _movement.RefreshMovementSpeedModifiers(user);
                    _evasion.RefreshEvasionModifiers(user);
                    break;
                case STOrderTypes.Hold:
                    var holdOrder = Comp<HoldOrderComponent>(user);
                    holdOrder.DamageModifier *= 1.25;
                    holdOrder.PainModifier *= 1.25;
                    break;
                case STOrderTypes.Focus:
                    var focusOrder = Comp<FocusOrderComponent>(user);
                    focusOrder.AccuracyModifier *= 1.25;
                    focusOrder.AccuracyPerTileModifier *= 1.25;
                    break;
            }
        }
        else
            _audio.PlayPredicted(emitOnUseComp.Sound, user, user);

        TryWhistle(whistle.Value, user);
    }
    #endregion

    private void OnGetItemActions(Entity<RMCWhistleComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags == SlotFlags.POCKET)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
    }

    public void OnWhistleAction(Entity<RMCWhistleComponent> ent, ref SoundActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Handled)
            return;

        TryWhistle(ent, args.Performer);
        args.Handled = true;
    }

    public void OnUseInHand(Entity<RMCWhistleComponent> ent, ref UseInHandEvent args)
    {
        TryWhistle(ent, args.User);
        args.Handled = true;
    }

    public void TryWhistle(Entity<RMCWhistleComponent> ent, EntityUid user)
    {
        _whistle.TryMakeLoudWhistle(ent, user);

        if (TryComp<UseDelayComponent>(ent, out var useDelay))
        {
            _actions.SetCooldown(ent.Comp.Action, useDelay.Delay);
            _useDelay.SetLength(ent.Owner, useDelay.Delay);
            _useDelay.TryResetDelay((ent.Owner, useDelay));
        }
    }
}
