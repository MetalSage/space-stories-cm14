using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Content.Shared.Interaction.Events;

namespace Content.Shared._Stories.TransformableItem;

public abstract class SharedTransformableItemSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformableItemComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<TransformableItemComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Transformations.Count == 0)
            return;

        args.Handled = true;
        _ui.OpenUi(ent.Owner, TransformableItemUiKey.Key, args.User);
    }
}
