using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Shared.Interaction
{
    public sealed class ExtendedInteractionRangeSystem : EntitySystem
    {
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedInteractionSystem _interaction = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<InRangeOverrideEvent>(OnInRangeOverride);
        }

        private void OnInRangeOverride(ref InRangeOverrideEvent args)
        {
            if (args.Handled)
                return;

            if (!_hands.TryGetActiveItem(args.User, out var item))
                return;

            if (!TryComp<ExtendedInteractionRangeComponent>(item, out var rangeComp))
                return;

            if (!HasComp<MobStateComponent>(args.Target))
                return;
            
            var userPos = _transform.GetMapCoordinates(args.User);
            var targetPos = _transform.GetMapCoordinates(args.Target);

            bool inRange = _interaction.InRangeUnobstructed(
                userPos, 
                targetPos, 
                range: rangeComp.Range
            );

            if (inRange)
            {
                args.InRange = true;
                args.Handled = true;
            }
        }
    }
}
