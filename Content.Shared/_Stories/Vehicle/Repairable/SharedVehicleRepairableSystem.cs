using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Repairable;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Stories.Vehicle.Repairable;

public sealed class SharedVehicleRepairableSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly RMCRepairableSystem _rmcRepairable = default!;
    [Dependency] private readonly SharedRMCDamageableSystem _rmcDamageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleRepairableComponent, InteractUsingEvent>(OnRepairableInteractUsing);
        SubscribeLocalEvent<VehicleRepairableComponent, RMCRepairableDoAfterEvent>(OnRepairableDoAfter);
    }

    private void OnRepairableInteractUsing(Entity<VehicleRepairableComponent> repairable, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<ToolComponent>(args.Used))
            return;

        if (!TryComp<DamageableComponent>(repairable, out var damageable))
            return;

        if (_tool.HasQuality(args.Used, "Welding") &&
            HasComp<BlowtorchComponent>(args.Used) &&
            !_rmcRepairable.UseFuel(args.Used, args.User, repairable.Comp.FuelUsed, true))
            return;

        if (damageable.TotalDamage <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("rmc-repairable-not-damaged", ("target", repairable)),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var (currentHp, maxHp) = GetHealthValues(repairable, damageable);
        if (maxHp <= 0)
            return;

        var ratio = currentHp / maxHp;
        var tool = GetRequiredTool(repairable.Comp, ratio);

        if (tool is null || !_tool.HasQuality(args.Used, tool.Value))
            return;

        args.Handled = true;

        var ev = new RMCRepairableDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, repairable.Comp.Delay, ev, repairable, used: args.Used)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            string msg = "Repair";
            if (repairable.Comp.ToolMessages != null &&
                repairable.Comp.ToolMessages.TryGetValue(tool.Value, out var custom))
            {
                msg = custom;
            }

            _popup.PopupPredicted(msg, msg, args.User, args.User);
        }
    }

    private void OnRepairableDoAfter(Entity<VehicleRepairableComponent> repairable, ref RMCRepairableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used == null)
            return;

        args.Handled = true;

        if (!TryComp<DamageableComponent>(repairable, out var damageable))
            return;

        if (_tool.HasQuality(args.Used.Value, "Welding") &&
            HasComp<BlowtorchComponent>(args.Used.Value) &&
            !_rmcRepairable.UseFuel(args.Used.Value, args.User, repairable.Comp.FuelUsed))
        {
            return;
        }

        var toolUseEv = new ToolUseAttemptEvent(args.User, (float)repairable.Comp.FuelUsed);
        RaiseLocalEvent(args.Used.Value, toolUseEv);
        if (toolUseEv.Cancelled)
            return;

        var (currentHp, maxHp) = GetHealthValues(repairable, damageable);
        if (maxHp <= 0)
            return;

        var ratio = currentHp / maxHp;
        var tool = GetRequiredTool(repairable.Comp, ratio);

        if (tool is null || !_tool.HasQuality(args.Used.Value, tool.Value))
            return;

        var amountFixedAdjustment = _skills.GetSkillDelayMultiplier(args.User, repairable.Comp.Skill);

        float healAmount;
        if (TryComp<VehicleAttachableComponent>(repairable, out var vehicleAttachable) &&
            _proto.TryIndex<HardpointTypePrototype>(vehicleAttachable.HardpointType, out var proto))
        {
            var repairPerSecond = proto.RepairPerSecond;
            healAmount = maxHp / 100f * (repairPerSecond / amountFixedAdjustment); // hardpoint.dm -> initial_health / 100 * (amount_fixed / amount_fixed_adjustment)
        }
        else
        {
            healAmount = maxHp / 100f * (5f / amountFixedAdjustment); // multitile_interaction.dm -> health = min(health + max_hp/100 * (5 / amount_fixed_adjustment), max_hp)
        }

        var heal = -_rmcDamageable.DistributeTypesTotal(repairable.Owner, healAmount);
        _damageable.TryChangeDamage(repairable, heal, true);

        _popup.PopupPredicted("Repair completed", "Repair completed other", args.User, args.User);

        if (repairable.Comp.ToolSoundThresholds != null &&
            repairable.Comp.ToolSoundThresholds.TryGetValue(tool.Value, out var toolSound))
        {
            _audio.PlayPredicted(toolSound, repairable.Owner, args.User);
        }

        if (damageable.TotalDamage > FixedPoint2.Zero)
        {
            var ev = new RMCRepairableDoAfterEvent();
            var doAfter = new DoAfterArgs(EntityManager, args.User, repairable.Comp.Delay, ev, repairable, used: args.Used.Value)
            {
                BreakOnMove = true,
                BlockDuplicate = true,
                DuplicateCondition = DuplicateConditions.SameEvent
            };
            _doAfter.TryStartDoAfter(doAfter);
        }
    }

    private (float currentHp, float maxHp) GetHealthValues(EntityUid entity, DamageableComponent damageable)
    {
        if (TryComp<VehicleComponent>(entity, out var vehicle))
        {
            var currentHp = MathF.Max(0f, (float)(vehicle.MaxHealth - damageable.TotalDamage));
            var maxHp = (float)vehicle.MaxHealth;

            return (currentHp, maxHp);
        }
        else if (TryComp<VehicleAttachableComponent>(entity, out var attachable))
        {
            var currentHp = MathF.Max(0f, (float)(attachable.MaxHealth - damageable.TotalDamage));
            var maxHp = (float)attachable.MaxHealth;

            return (currentHp, maxHp);
        }

        return (0f, 0f);
    }

    private ProtoId<ToolQualityPrototype>? GetRequiredTool(VehicleRepairableComponent comp, float ratio)
    {
        ProtoId<ToolQualityPrototype>? best = null;
        var bestThreshold = float.MinValue;

        foreach (var kv in comp.ToolThresholds)
        {
            if (ratio >= kv.Value && kv.Value >= bestThreshold)
            {
                best = kv.Key;
                bestThreshold = kv.Value;
            }
        }

        return best;
    }
}
