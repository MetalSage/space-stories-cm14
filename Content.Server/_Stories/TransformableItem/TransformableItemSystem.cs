using Content.Shared._RMC14.Vendors;
using Content.Shared._Stories.TransformableItem;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.TransformableItem;

public sealed class TransformableItemSystem : SharedTransformableItemSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<TransformableItemComponent>(TransformableItemUiKey.Key, subs =>
        {
            subs.Event<TransformableItemBuiMsg>(OnTransformBui);
        });
    }

    private void OnTransformBui(Entity<TransformableItemComponent> ent, ref TransformableItemBuiMsg args)
    {
        var user = args.Actor;

        if (!ent.Comp.Transformations.Contains(args.TransformationId))
            return;

        if (!_hands.IsHolding(user, ent, out _))
            return;

        RemComp<TransformableItemComponent>(ent);

        if (!_prototypeManager.TryIndex(args.TransformationId, out var proto))
        {
            Log.Error($"Invalid prototype '{args.TransformationId}' sent in TransformableItemBuiMsg.");
            return;
        }

        if (proto.TryGetComponent<CMChangeUserOnVendComponent>(out var change, _compFactory) &&
            change.AddComponents != null)
        {
            EntityManager.AddComponents(user, change.AddComponents);
        }

        var userXform = Transform(user);

        QueueDel(ent);
        var newEntity = Spawn(args.TransformationId, userXform.Coordinates);
        
        _hands.TryPickupAnyHand(user, newEntity, checkActionBlocker: false);
    }
}
