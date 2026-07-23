using Content.Shared._Stories.Synth.VoiceSynthesizer;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Stories.Synth.WorkingJoe;

public sealed class STWorkingJoeVariantSystem : EntitySystem
{
    private const string JobId = "STJobWorkingJoe";
    private const string HazmatVariant = "Hazmat";
    private const string HazmatJumpsuit = "STJumpsuitHazmatJoe";

    private const string HazmatOverallsJumpsuit = "STJumpsuitHazmatJoeOveralls";

    private static readonly string[] DependentSlots = { "pocket1", "pocket2", "belt" };

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId != JobId)
            return;

        if (!args.Profile.VariantPreferences.TryGetValue(JobId, out var variant) || variant != HazmatVariant)
            return;

        var mob = args.Mob;

        var held = new List<(string Slot, EntityUid Item)>();
        foreach (var slot in DependentSlots)
        {
            if (_inventory.TryUnequip(mob, slot, out var item, force: true) && item is { } heldItem)
                held.Add((slot, heldItem));
        }

        if (_inventory.TryUnequip(mob, "jumpsuit", out var oldJumpsuit, force: true) && oldJumpsuit is { } old)
            QueueDel(old);

        var jumpsuitProto = _random.Prob(0.5f) ? HazmatOverallsJumpsuit : HazmatJumpsuit;
        var jumpsuit = Spawn(jumpsuitProto, MapCoordinates.Nullspace);
        _inventory.TryEquip(mob, jumpsuit, "jumpsuit", force: true);

        foreach (var (slot, item) in held)
        {
            _inventory.TryEquip(mob, item, slot, force: true);
        }

        _movementSpeed.RefreshMovementSpeedModifiers(mob);

        if (TryComp<STSynthVoiceSynthesizerComponent>(mob, out var voice))
        {
            voice.UseAlternateSound = true;
            Dirty(mob, voice);
        }
    }
}
