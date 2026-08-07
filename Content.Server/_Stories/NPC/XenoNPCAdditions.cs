using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Threading.Tasks;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Xeno
{
    [DataDefinition]
    public sealed partial class SetCoordinatesFromTargetOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entManager = default!;

        [DataField(required: true)]
        public string TargetKey = "Target";

        [DataField(required: true)]
        public string CoordinatesKey = "TargetCoordinates";

        public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
            System.Threading.CancellationToken cancelToken)
        {
            if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) || !_entManager.HasComponent<TransformComponent>(target))
                return (false, null);

            var xform = _entManager.GetComponent<TransformComponent>(target);

            var effects = new Dictionary<string, object>
            {
                { CoordinatesKey, xform.Coordinates }
            };

            return (true, effects);
        }
    }
}

namespace Content.Server.NPC.HTN.Preconditions.Xeno
{
    [DataDefinition]
    public sealed partial class TargetHasComponentPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly IComponentFactory _compFactory = default!;

        [DataField(required: true)]
        public string TargetKey = "Target";

        [DataField(required: true)]
        public string Component = default!;

        [DataField]
        public bool Invert = false;

        private Type? _compType;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            if (_compFactory.TryGetRegistration(Component, out var registration))
            {
                _compType = registration.Type;
            }
        }

        public override bool IsMet(NPCBlackboard blackboard)
        {
            if (_compType == null)
                return Invert;

            if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
                return Invert;

            var hasComponent = _entManager.HasComponent(target, _compType);
            return hasComponent != Invert;
        }
    }

    [DataDefinition]
    public sealed partial class IsOnWeedsPrecondition : HTNPrecondition
    {
        [Dependency] private readonly IEntityManager _entManager = default!;
        private EntityLookupSystem _lookup = default!;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        }
        
        [DataField]
        public bool Invert = false;

        public override bool IsMet(NPCBlackboard blackboard)
        {
            var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
            var xform = _entManager.GetComponent<TransformComponent>(owner);
            
            var found = false;
            foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, 0.1f))
            {
                if (_entManager.HasComponent<XenoWeedsComponent>(entity))
                {
                    found = true;
                    break;
                }
            }

            return found != Invert;
        }
    }

    [DataDefinition]
    public sealed partial class NearbyWeedsPrecondition : HTNPrecondition
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
                if (_entManager.TryGetComponent<XenoWeedsComponent>(entity, out var weeds) && weeds.IsSource)
                {
                    found = true;
                    break;
                }
            }
            return found != Invert;
        }
    }
}
