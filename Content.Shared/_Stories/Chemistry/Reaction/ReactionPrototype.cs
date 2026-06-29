// ReSharper disable CheckNamespace
using Content.Shared._Stories.Chemistry.Reaction;

namespace Content.Shared.Chemistry.Reaction
{
    public sealed partial class ReactionPrototype
    {
        [DataField]
        public StoriesChemReactionFlags ReactionFlags = StoriesChemReactionFlags.Calm;

        [DataField]
        public StoriesReactionConfig? StoriesConfig;
    }
}
