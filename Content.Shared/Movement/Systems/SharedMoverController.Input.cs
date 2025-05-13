using System.Numerics;
using Content.Shared.Alert;
using Content.Shared.CCVar;
using Content.Shared.Follower.Components;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared._Stories.APC;

namespace Content.Shared.Movement.Systems
{
    public abstract partial class SharedMoverController
    {
        public bool CameraRotationLocked { get; set; }

        public static ProtoId<AlertPrototype> WalkingAlert = "Walking";

        private void InitializeInput()
        {
            var moveUpCmdHandler = new MoverDirInputCmdHandler(this, Direction.North);
            var moveLeftCmdHandler = new MoverDirInputCmdHandler(this, Direction.West);
            var moveRightCmdHandler = new MoverDirInputCmdHandler(this, Direction.East);
            var moveDownCmdHandler = new MoverDirInputCmdHandler(this, Direction.South);

            CommandBinds.Builder
                .Bind(EngineKeyFunctions.MoveUp, moveUpCmdHandler)
                .Bind(EngineKeyFunctions.MoveLeft, moveLeftCmdHandler)
                .Bind(EngineKeyFunctions.MoveRight, moveRightCmdHandler)
                .Bind(EngineKeyFunctions.MoveDown, moveDownCmdHandler)
                .Bind(EngineKeyFunctions.Walk, new WalkInputCmdHandler(this))
                .Bind(EngineKeyFunctions.CameraRotateLeft, new CameraRotateInputCmdHandler(this, Direction.East))
                .Bind(EngineKeyFunctions.CameraRotateRight, new CameraRotateInputCmdHandler(this, Direction.West))
                .Bind(EngineKeyFunctions.CameraReset, new CameraResetInputCmdHandler(this))
                .Bind(ContentKeyFunctions.ShuttleStrafeUp, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeUp))
                .Bind(ContentKeyFunctions.ShuttleStrafeLeft, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeLeft))
                .Bind(ContentKeyFunctions.ShuttleStrafeRight, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeRight))
                .Bind(ContentKeyFunctions.ShuttleStrafeDown, new ShuttleInputCmdHandler(this, ShuttleButtons.StrafeDown))
                .Bind(ContentKeyFunctions.ShuttleRotateLeft, new ShuttleInputCmdHandler(this, ShuttleButtons.RotateLeft))
                .Bind(ContentKeyFunctions.ShuttleRotateRight, new ShuttleInputCmdHandler(this, ShuttleButtons.RotateRight))
                .Bind(ContentKeyFunctions.ShuttleBrake, new ShuttleInputCmdHandler(this, ShuttleButtons.Brake))
                .Register<SharedMoverController>();

            SubscribeLocalEvent<InputMoverComponent, ComponentInit>(OnInputInit);
            SubscribeLocalEvent<InputMoverComponent, ComponentGetState>(OnMoverGetState);
            SubscribeLocalEvent<InputMoverComponent, ComponentHandleState>(OnMoverHandleState);
            SubscribeLocalEvent<InputMoverComponent, EntParentChangedMessage>(OnInputParentChange);

            SubscribeLocalEvent<FollowedComponent, EntParentChangedMessage>(OnFollowedParentChange);

            Subs.CVar(_configManager, CCVars.CameraRotationLocked, obj => CameraRotationLocked = obj, true);
            Subs.CVar(_configManager, CCVars.GameDiagonalMovement, value => DiagonalMovementEnabled = value, true);
        }

        public static MoveButtons GetNormalizedMovement(MoveButtons buttons)
        {
            var oldMovement = buttons;

            if ((oldMovement & (MoveButtons.Left | MoveButtons.Right)) == (MoveButtons.Left | MoveButtons.Right))
            {
                oldMovement &= ~MoveButtons.Left;
                oldMovement &= ~MoveButtons.Right;
            }

            if ((oldMovement & (MoveButtons.Up | MoveButtons.Down)) == (MoveButtons.Up | MoveButtons.Down))
            {
                oldMovement &= ~MoveButtons.Up;
                oldMovement &= ~MoveButtons.Down;
            }

            return oldMovement;
        }

        protected void SetMoveInput(Entity<InputMoverComponent> entity, MoveButtons buttons)
        {
            if (entity.Comp.HeldMoveButtons == buttons)
                return;

            var moveEvent = new MoveInputEvent(entity, entity.Comp.HeldMoveButtons);
            entity.Comp.HeldMoveButtons = buttons;
            RaiseLocalEvent(entity, ref moveEvent);
            Dirty(entity, entity.Comp);

            var ev = new SpriteMoveEvent(entity.Comp.HeldMoveButtons != MoveButtons.None);
            RaiseLocalEvent(entity, ref ev);
        }

        private void OnMoverHandleState(Entity<InputMoverComponent> entity, ref ComponentHandleState args)
        {
            if (args.Current is not InputMoverComponentState state)
                return;

            entity.Comp.LerpTarget = state.LerpTarget;
            entity.Comp.RelativeRotation = state.RelativeRotation;
            entity.Comp.TargetRelativeRotation = state.TargetRelativeRotation;
            entity.Comp.CanMove = state.CanMove;
            entity.Comp.RelativeEntity = EnsureEntity<InputMoverComponent>(state.RelativeEntity, entity.Owner);

            entity.Comp.LastInputTick = GameTick.Zero;
            entity.Comp.LastInputSubTick = 0;

            if (entity.Comp.HeldMoveButtons != state.HeldMoveButtons)
            {
                var moveEvent = new MoveInputEvent(entity, entity.Comp.HeldMoveButtons);
                entity.Comp.HeldMoveButtons = state.HeldMoveButtons;
                RaiseLocalEvent(entity.Owner, ref moveEvent);

                var ev = new SpriteMoveEvent(entity.Comp.HeldMoveButtons != MoveButtons.None);
                RaiseLocalEvent(entity, ref ev);
            }
        }

        private void OnMoverGetState(Entity<InputMoverComponent> entity, ref ComponentGetState args)
        {
            args.State = new InputMoverComponentState()
            {
                CanMove = entity.Comp.CanMove,
                RelativeEntity = GetNetEntity(entity.Comp.RelativeEntity),
                LerpTarget = entity.Comp.LerpTarget,
                HeldMoveButtons = entity.Comp.HeldMoveButtons,
                RelativeRotation = entity.Comp.RelativeRotation,
                TargetRelativeRotation = entity.Comp.TargetRelativeRotation,
            };
        }

        private void ShutdownInput()
        {
            CommandBinds.Unregister<SharedMoverController>();
        }

        public bool DiagonalMovementEnabled { get; private set; }

        protected virtual void HandleShuttleInput(EntityUid uid, ShuttleButtons button, ushort subTick, bool state) {}

        public void RotateCamera(EntityUid uid, Angle angle)
        {
            if (CameraRotationLocked || !MoverQuery.TryGetComponent(uid, out var mover))
                return;

            mover.TargetRelativeRotation += angle;
            Dirty(uid, mover);
        }

        public void ResetCamera(EntityUid uid)
        {
            if (CameraRotationLocked ||
                !MoverQuery.TryGetComponent(uid, out var mover))
            {
                return;
            }

            if (!TryUpdateRelative(mover, XformQuery.GetComponent(uid)) && mover.TargetRelativeRotation.Equals(Angle.Zero))
                return;

            mover.LerpTarget = TimeSpan.Zero;
            mover.TargetRelativeRotation = Angle.Zero;
            Dirty(uid, mover);
        }

        private bool TryUpdateRelative(InputMoverComponent mover, TransformComponent xform)
        {
            var relative = xform.GridUid;
            relative ??= xform.MapUid;

            if (mover.RelativeEntity.Equals(relative))
                return false;

            var currentRotation = Angle.Zero;
            var targetRotation = Angle.Zero;

            if (XformQuery.TryGetComponent(mover.RelativeEntity, out var oldRelativeXform))
            {
                currentRotation = _transform.GetWorldRotation(oldRelativeXform, XformQuery) + mover.RelativeRotation;
            }

            if (XformQuery.TryGetComponent(relative, out var relativeXform))
            {
                mover.RelativeRotation = (currentRotation - _transform.GetWorldRotation(relativeXform)).FlipPositive();
            }

            if (relative != null && HasComp<MapComponent>(relative.Value))
            {
                targetRotation = currentRotation.FlipPositive().Reduced();
            }
            else if (relative != null && _mapManager.IsGrid(relative.Value))
            {
                if (CameraRotationLocked)
                    targetRotation = Angle.Zero;
                else
                    targetRotation = mover.RelativeRotation.GetCardinalDir().ToAngle().Reduced();
            }

            mover.RelativeEntity = relative;
            mover.TargetRelativeRotation = targetRotation;
            return true;
        }

        public Angle GetParentGridAngle(InputMoverComponent mover)
        {
            var rotation = mover.RelativeRotation;

            if (XformQuery.TryGetComponent(mover.RelativeEntity, out var relativeXform))
                return _transform.GetWorldRotation(relativeXform) + rotation;

            return rotation;
        }

        private void OnFollowedParentChange(Entity<FollowedComponent> entity, ref EntParentChangedMessage args)
        {
            foreach (var foll in entity.Comp.Following)
            {
                if (!MoverQuery.TryGetComponent(foll, out var mover))
                    continue;

                var ev = new EntParentChangedMessage(foll, null, args.OldMapId, XformQuery.GetComponent(foll));
                OnInputParentChange((foll, mover), ref ev);
            }
        }

        private void OnInputParentChange(Entity<InputMoverComponent> entity, ref EntParentChangedMessage args)
        {
            var relative = args.Transform.GridUid;
            relative ??= args.Transform.MapUid;

            if (entity.Comp.LifeStage < ComponentLifeStage.Running)
            {
                entity.Comp.RelativeEntity = relative;
                Dirty(entity.Owner, entity.Comp);
                return;
            }

            var oldMapId = args.OldMapId;
            var mapId = args.Transform.MapUid;

            if (oldMapId != mapId)
            {
                entity.Comp.RelativeEntity = relative;
                entity.Comp.TargetRelativeRotation = Angle.Zero;
                entity.Comp.RelativeRotation = Angle.Zero;
                entity.Comp.LerpTarget = TimeSpan.Zero;
                Dirty(entity.Owner, entity.Comp);
                return;
            }

            if (relative == entity.Comp.RelativeEntity)
            {
                if (entity.Comp.LerpTarget >= Timing.CurTime)
                {
                    entity.Comp.LerpTarget = TimeSpan.Zero;
                    Dirty(entity.Owner, entity.Comp);
                }

                return;
            }

            entity.Comp.LerpTarget = TimeSpan.FromSeconds(InputMoverComponent.LerpTime) + Timing.CurTime;
            Dirty(entity.Owner, entity.Comp);
        }

        private void HandleDirChange(EntityUid entity, Direction dir, ushort subTick, bool state)
        {
            if (TryComp<RelayInputMoverComponent>(entity, out var relayMover))
            {
                if (MoverQuery.TryGetComponent(entity, out var mover))
                    SetMoveInput((entity, mover), MoveButtons.None);

                if (!_mobState.IsIncapacitated(entity))
                    HandleDirChange(relayMover.RelayEntity, dir, subTick, state);

                return;
            }

            if (!MoverQuery.TryGetComponent(entity, out var moverComp))
                return;

            if (_container.IsEntityInContainer(entity) &&
                TryComp(entity, out TransformComponent? xform) &&
                xform.ParentUid.IsValid() &&
                _mobState.IsAlive(entity))
            {
                var relayMoveEvent = new ContainerRelayMovementEntityEvent(entity);
                RaiseLocalEvent(xform.ParentUid, ref relayMoveEvent);
            }

            SetVelocityDirection((entity, moverComp), dir, subTick, state);
        }

        private void OnInputInit(Entity<InputMoverComponent> entity, ref ComponentInit args)
        {
            var xform = Transform(entity.Owner);

            if (!xform.ParentUid.IsValid())
                return;

            entity.Comp.RelativeEntity = xform.GridUid ?? xform.MapUid;
            entity.Comp.TargetRelativeRotation = Angle.Zero;
        }

        private void HandleRunChange(EntityUid uid, ushort subTick, bool walking)
        {
            MoverQuery.TryGetComponent(uid, out var moverComp);

            if (TryComp<RelayInputMoverComponent>(uid, out var relayMover))
            {
                if (moverComp != null)
                {
                    SetMoveInput((uid, moverComp), MoveButtons.None);
                }

                HandleRunChange(relayMover.RelayEntity, subTick, walking);
                return;
            }

            if (moverComp == null) return;

            SetSprinting((uid, moverComp), subTick, walking);
        }

        public (Vector2 Walking, Vector2 Sprinting) GetVelocityInput(InputMoverComponent mover)
        {
            if (!Timing.InSimulation)
            {
                var immediateDir = DirVecForButtons(mover.HeldMoveButtons);

                // Если APC и движемся задом — снизить скорость
                if (TryComp(mover.Owner, out APCEntityComponent _) &&
                    TryComp(mover.Owner, out TransformComponent xform))
                {
                    var facingDir = xform.LocalRotation.ToVec(); // Направление "вперёд"
                    if (Vector2.Dot(immediateDir, facingDir) < -0.9f) // Почти строго назад
                    {
                        immediateDir /= 1.5f;
                    }
                }

                return mover.Sprinting ? (Vector2.Zero, immediateDir) : (immediateDir, Vector2.Zero);
            }

            // Иначе – обычная логика
            Vector2 walk;
            Vector2 sprint;
            float remainingFraction;

            if (Timing.CurTick > mover.LastInputTick)
            {
                walk = Vector2.Zero;
                sprint = Vector2.Zero;
                remainingFraction = 1;
            }
            else
            {
                walk = mover.CurTickWalkMovement;
                sprint = mover.CurTickSprintMovement;
                remainingFraction = (ushort.MaxValue - mover.LastInputSubTick) / (float)ushort.MaxValue;
            }

            var curDir = DirVecForButtons(mover.HeldMoveButtons) * remainingFraction;

            if (TryComp(mover.Owner, out APCEntityComponent _) &&
                TryComp(mover.Owner, out TransformComponent xform2))
            {
                var facingDir = xform2.LocalRotation.ToVec();
                if (Vector2.Dot(curDir, facingDir) < -0.9f)
                {
                    curDir /= 1.5f;
                }
            }

            if (mover.Sprinting)
                sprint += curDir;
            else
                walk += curDir;

            return (walk, sprint);
        }

        public void SetVelocityDirection(Entity<InputMoverComponent> entity, Direction direction, ushort subTick, bool enabled)
        {
            var bit = direction switch
            {
                Direction.East => MoveButtons.Right,
                Direction.North => MoveButtons.Up,
                Direction.West => MoveButtons.Left,
                Direction.South => MoveButtons.Down,
                _ => throw new ArgumentException(nameof(direction))
            };

            SetMoveInput(entity, subTick, enabled, bit);
        }

        private void SetMoveInput(Entity<InputMoverComponent> entity, ushort subTick, bool enabled, MoveButtons bit)
        {
            ResetSubtick(entity.Comp);

            if (subTick >= entity.Comp.LastInputSubTick)
            {
                var fraction = (subTick - entity.Comp.LastInputSubTick) / (float) ushort.MaxValue;

                ref var lastMoveAmount = ref entity.Comp.Sprinting ? ref entity.Comp.CurTickSprintMovement : ref entity.Comp.CurTickWalkMovement;

                lastMoveAmount += DirVecForButtons(entity.Comp.HeldMoveButtons) * fraction;

                entity.Comp.LastInputSubTick = subTick;
            }

            var buttons = entity.Comp.HeldMoveButtons;

            if (enabled)
            {
                buttons |= bit;
            }
            else
            {
                buttons &= ~bit;
            }

            SetMoveInput(entity, buttons);
        }

        private void ResetSubtick(InputMoverComponent component)
        {
            if (Timing.CurTick <= component.LastInputTick) return;

            component.CurTickWalkMovement = Vector2.Zero;
            component.CurTickSprintMovement = Vector2.Zero;
            component.LastInputTick = Timing.CurTick;
            component.LastInputSubTick = 0;
        }

        public virtual void SetSprinting(Entity<InputMoverComponent> entity, ushort subTick, bool walking)
        {
            SetMoveInput(entity, subTick, walking, MoveButtons.Walk);
        }

        private Vector2 DirVecForButtons(MoveButtons buttons)
        {
            var x = 0;
            x -= HasFlag(buttons, MoveButtons.Left) ? 1 : 0;
            x += HasFlag(buttons, MoveButtons.Right) ? 1 : 0;

            var y = 0;

            bool noDiagonalMovement = false;
            if (Session?.AttachedEntity != null && HasComp<RestrictDiagonalMovementComponent>(Session.AttachedEntity.Value))
            {
                noDiagonalMovement = true;
            }

            if (!DiagonalMovementEnabled || noDiagonalMovement)
            {
                if (x != 0)
                {
                    y = 0;
                }
                else
                {
                    y -= HasFlag(buttons, MoveButtons.Down) ? 1 : 0;
                    y += HasFlag(buttons, MoveButtons.Up) ? 1 : 0;
                }
            }
            else
            {
                y -= HasFlag(buttons, MoveButtons.Down) ? 1 : 0;
                y += HasFlag(buttons, MoveButtons.Up) ? 1 : 0;
            }

            var vec = new Vector2(x, y);

            if (vec.LengthSquared() > 1.0e-6)
            {
                vec = vec.Normalized();
            }

            return vec;
        }

        private static bool HasFlag(MoveButtons buttons, MoveButtons flag)
        {
            return (buttons & flag) == flag;
        }

        private ICommonSession? Session => _playerManager.LocalSession;

        private sealed class CameraRotateInputCmdHandler : InputCmdHandler
        {
            private readonly SharedMoverController _controller;
            private readonly Angle _angle;

            public CameraRotateInputCmdHandler(SharedMoverController controller, Direction direction)
            {
                _controller = controller;
                _angle = direction.ToAngle();
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity == null) return false;

                if (message.State != BoundKeyState.Up)
                    return false;

                _controller.RotateCamera(session.AttachedEntity.Value, _angle);
                return false;
            }
        }

        private sealed class CameraResetInputCmdHandler : InputCmdHandler
        {
            private readonly SharedMoverController _controller;

            public CameraResetInputCmdHandler(SharedMoverController controller)
            {
                _controller = controller;
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity == null) return false;

                if (message.State != BoundKeyState.Up)
                    return false;

                _controller.ResetCamera(session.AttachedEntity.Value);
                return false;
            }
        }

        private sealed class MoverDirInputCmdHandler : InputCmdHandler
        {
            private readonly SharedMoverController _controller;
            private readonly Direction _dir;

            public MoverDirInputCmdHandler(SharedMoverController controller, Direction dir)
            {
                _controller = controller;
                _dir = dir;
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity == null) return false;

                _controller.HandleDirChange(session.AttachedEntity.Value, _dir, message.SubTick, message.State == BoundKeyState.Down);
                return false;
            }
        }

        private sealed class WalkInputCmdHandler : InputCmdHandler
        {
            private SharedMoverController _controller;

            public WalkInputCmdHandler(SharedMoverController controller)
            {
                _controller = controller;
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity == null) return false;

                _controller.HandleRunChange(session.AttachedEntity.Value, message.SubTick, message.State == BoundKeyState.Down);
                return false;
            }
        }

        private sealed class ShuttleInputCmdHandler : InputCmdHandler
        {
            private readonly SharedMoverController _controller;
            private readonly ShuttleButtons _button;

            public ShuttleInputCmdHandler(SharedMoverController controller, ShuttleButtons button)
            {
                _controller = controller;
                _button = button;
            }

            public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
            {
                if (session?.AttachedEntity == null) return false;

                _controller.HandleShuttleInput(session.AttachedEntity.Value, _button, message.SubTick, message.State == BoundKeyState.Down);
                return false;
            }
        }
    }

    [Flags]
    [Serializable, NetSerializable]
    public enum MoveButtons : byte
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,
        Walk = 16,
        AnyDirection = Up | Down | Left | Right,
    }

    [Flags]
    public enum ShuttleButtons : byte
    {
        None = 0,
        StrafeUp = 1 << 0,
        StrafeDown = 1 << 1,
        StrafeLeft = 1 << 2,
        StrafeRight = 1 << 3,
        RotateLeft = 1 << 4,
        RotateRight = 1 << 5,
        Brake = 1 << 6,
    }
}