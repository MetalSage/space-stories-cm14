using Content.Server.NPC.HTN.Preconditions;
using Content.Shared.Examine;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.NPC.HTN.Preconditions.Xeno
{
    [DataDefinition]
    public sealed partial class TargetInLOSPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private ExamineSystemShared _examine = default!;

        [DataField(required: true)]
        public string TargetKey = "Target";

        [DataField]
        public bool Invert = false;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _examine = sysManager.GetEntitySystem<ExamineSystemShared>();
        }

        public override bool IsMet(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            
            if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
                return Invert;

            var radius = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entManager), _entManager);
            var inLos = _examine.InRangeUnOccluded(owner, target, radius + 0.5f, null);

            return inLos != Invert;
        }
    }
}
