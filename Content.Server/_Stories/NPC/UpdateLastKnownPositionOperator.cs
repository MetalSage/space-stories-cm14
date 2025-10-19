using Content.Server.NPC.HTN;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Xeno
{
    [DataDefinition]
    public sealed partial class UpdateLastKnownPositionOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        [DataField(required: true)]
        public string TargetKey = "Target";

        [DataField(required: true)]
        public string LastKnownPositionKey = "LastKnownPosition";

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) &&
                _entManager.TryGetComponent<TransformComponent>(target, out var xform))
            {
                blackboard.SetValue(LastKnownPositionKey, xform.Coordinates);
            }
            return HTNOperatorStatus.Finished;
        }
    }
}
