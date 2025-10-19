using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using System.Linq;
using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared.Actions.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared._RMC14.Xenonids.Construction;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Threading.Tasks;
using System.Threading;

namespace Content.Server.NPC.HTN.Preconditions.Xeno
{
    [DataDefinition]
    public sealed partial class SelfHealthPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private MobThresholdSystem _thresholdSystem = default!;

        [DataField]
        public float MinHealthFraction = 0.0f;

        [DataField]
        public float MaxHealthFraction = 1.0f;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _thresholdSystem = sysManager.GetEntitySystem<MobThresholdSystem>();
        }

        public override bool IsMet(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

            if (!_entManager.TryGetComponent<DamageableComponent>(owner, out var damageable))
                return false;

            if (!_thresholdSystem.TryGetIncapPercentage(owner, damageable.TotalDamage, out var damageFraction))
                return false;

            var healthFraction = 1.0f - (float)damageFraction;

            return healthFraction >= MinHealthFraction && healthFraction <= MaxHealthFraction;
        }
    }

    [DataDefinition]
    public sealed partial class HasStatusPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        [DataField]
        public bool Invert = false;

        public override bool IsMet(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            var hasComp = _entManager.HasComponent<XenoRestingComponent>(owner);
            return hasComp != Invert;
        }
    }

    [DataDefinition]
    public sealed partial class NearbyResinPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private EntityLookupSystem _lookup = default!;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        }

        [DataField(required: true)]
        public float Range;

        [DataField]
        public bool Invert = false;

        public override bool IsMet(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            var xform = _entManager.GetComponent<TransformComponent>(owner);

            var found = false;
            foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, Range))
            {
                if (_entManager.HasComponent<XenoConstructComponent>(entity))
                {
                    found = true;
                    break;
                }
            }

            return found != Invert;
        }
    }
}

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Xeno
{
    [DataDefinition]
    public sealed partial class FleeOperator : HTNOperator, IHtnConditionalShutdown
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;

        private NPCSteeringSystem _steering = default!;
        private PathfindingSystem _pathfinding = default!;
        private SharedTransformSystem _transform = default!;
        private SharedMapSystem _mapSystem = default!;

        [DataField(required: true)]
        public string TargetKey = string.Empty;

        [DataField]
        public string FleeTargetKey = "FleeTarget";

        [DataField(required: true)]
        public string DistanceKey = string.Empty;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
            _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
            _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
            _mapSystem = sysManager.GetEntitySystem<SharedMapSystem>();
        }

        public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
        {
            if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
                return (false, null);

            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            if (!_entManager.TryGetComponent<TransformComponent>(owner, out var ownerXform))
                return (false, null);

            EntityCoordinates finalCoordinates;

            if (blackboard.TryGetValue<EntityUid>(FleeTargetKey, out var fleeTarget, _entManager) &&
                _entManager.TryGetComponent<TransformComponent>(fleeTarget, out var fleeTargetXform))
            {
                finalCoordinates = fleeTargetXform.Coordinates;
            }
            else
            {
                if (!_entManager.TryGetComponent<TransformComponent>(target, out var targetXform))
                    return (false, null);

                var fleeDistance = blackboard.GetValueOrDefault<float>(DistanceKey, _entManager);
                var direction = ownerXform.WorldPosition - targetXform.WorldPosition;

                if (direction == Vector2.Zero)
                    direction = new Vector2(1, 0);

                var targetPosition = ownerXform.WorldPosition + direction.Normalized() * fleeDistance;

                if (_mapManager.TryFindGridAt(ownerXform.MapID, targetPosition, out var gridUid, out var mapGrid))
                {
                    finalCoordinates = new EntityCoordinates(gridUid, _mapSystem.WorldToLocal(gridUid, mapGrid, targetPosition));
                }
                else if (ownerXform.MapUid is { } mapUid)
                {
                    finalCoordinates = new EntityCoordinates(mapUid, targetPosition);
                }
                else
                {
                     return (false, null);
                }
            }

            var path = await _pathfinding.GetPath(owner, ownerXform.Coordinates, finalCoordinates, 1f, cancelToken);
            if (path.Result != PathResult.Path)
                return (false, null);

            var effects = new Dictionary<string, object>
            {
                { NPCBlackboard.MovementTarget, finalCoordinates },
                { NPCBlackboard.PathfindKey, path }
            };

            return (true, effects);
        }

        public override void Startup(NPCBlackboard blackboard)
        {
            base.Startup(blackboard);
            if (blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.MovementTarget, out var coordinates, _entManager))
            {
                var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
                var steering = _steering.Register(owner, coordinates);
                steering.Range = 2.0f;

                if (blackboard.TryGetValue<PathResultEvent>(NPCBlackboard.PathfindKey, out var path, _entManager))
                {
                    steering.CurrentPath = new Queue<PathPoly>(path.Path);
                }
            }
        }

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

            if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
                return HTNOperatorStatus.Failed;

            return steering.Status switch
            {
                SteeringStatus.InRange => HTNOperatorStatus.Finished,
                SteeringStatus.NoPath => HTNOperatorStatus.Failed,
                SteeringStatus.Moving => HTNOperatorStatus.Continuing,
                _ => HTNOperatorStatus.Failed
            };
        }

        public HTNPlanState ShutdownState => HTNPlanState.TaskFinished;

        public void ConditionalShutdown(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            _steering.Unregister(owner);
            blackboard.Remove<EntityCoordinates>(NPCBlackboard.MovementTarget);
            blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        }

        public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
        {
            base.TaskShutdown(blackboard, status);
            ConditionalShutdown(blackboard);
        }
    }

    [DataDefinition]
    public sealed partial class UseActionOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private SharedActionsSystem _actions = default!;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _actions = sysManager.GetEntitySystem<SharedActionsSystem>();
        }

        [DataField(required: true)]
        public EntProtoId ActionId = default!;

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

            if (!_entManager.TryGetComponent<ActionsComponent>(owner, out var actionsComponent))
                return HTNOperatorStatus.Failed;

            EntityUid? actionToPerform = null;
            foreach (var action in actionsComponent.Actions)
            {
                var meta = _entManager.GetComponent<MetaDataComponent>(action);

                if (meta.EntityPrototype != null && meta.EntityPrototype.ID == ActionId)
                {
                    actionToPerform = action;
                    break;
                }
            }

            if (actionToPerform == null)
                return HTNOperatorStatus.Failed;

            if (_actions.GetAction(actionToPerform.Value) is not { } actionData)
                return HTNOperatorStatus.Failed;

            _actions.PerformAction(owner, actionData);
            return HTNOperatorStatus.Finished;
        }
    }
}
