using Content.Shared._Stories.Hunter.Bracer.Components;

namespace Content.Shared._Stories.Hunter.Bracer;

public sealed partial class BracerSystem
{
    private void InitializeCrafting()
    {
        SubscribeLocalEvent<HunterBracerComponent, CreateHealingCapsuleEvent>(OnCreateHealingCapsule);
        SubscribeLocalEvent<HunterBracerComponent, CreateStabilizingCrystalEvent>(OnCreateStabilizingCrystal);
    }

    private void OnCreateHealingCapsule(Entity<HunterBracerComponent> ent, ref CreateHealingCapsuleEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!AttemptUsage(user, ent))
            return;

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupClient(Loc.GetString("st-bracer-craft-no-free-hand"), user, user);
            return;
        }

        if (!TryDrainPower(user, ent, ent.Comp.HealingCapsuleCost))
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            _audio.PlayPvs(ent.Comp.CasterModeCycleSound, user);
            var capsule = Spawn(ent.Comp.HealingCapsulePrototype, Transform(user).Coordinates);
            _hands.TryPickupAnyHand(user, capsule);

            _popup.PopupEntity(Loc.GetString("st-bracer-craft-capsule-success"), user, user);
        }
    }

    private void OnCreateStabilizingCrystal(Entity<HunterBracerComponent> ent, ref CreateStabilizingCrystalEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!AttemptUsage(user, ent))
            return;

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupClient(Loc.GetString("st-bracer-craft-no-free-hand"), user, user);
            return;
        }

        if (!TryDrainPower(user, ent, ent.Comp.StabilizingCrystalCost))
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            _audio.PlayPvs(ent.Comp.CasterModeCycleSound, user);
            var crystal = Spawn(ent.Comp.StabilizingCrystalPrototype, Transform(user).Coordinates);
            _hands.TryPickupAnyHand(user, crystal);

            _popup.PopupEntity(Loc.GetString("st-bracer-craft-crystal-success"), user, user);
        }
    }
}
