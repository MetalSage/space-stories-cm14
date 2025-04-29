using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Stories.ChasmTeleporter
{
    [NetworkedComponent, RegisterComponent, Access(typeof(ChasmTeleporterSystem))]
    public sealed partial class ChasmTeleporterComponent : Component
    {
        [DataField]
        public SoundSpecifier FallingSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");

        [DataField]
        public bool PlaySound = true;

        [DataField]
        public float Radius = 5f;

        [DataField]
        public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(1.5);

        [DataField]
        public string TargetName = string.Empty;
    }

    [RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
    public sealed partial class ChasmTeleporterFallingComponent : Component
    {
        [DataField]
        public TimeSpan AnimationTime = TimeSpan.FromSeconds(1.5f);

        [DataField]
        public TimeSpan DeletionTime = TimeSpan.FromSeconds(1.8f);

        [DataField(customTypeSerializer:typeof(TimeOffsetSerializer)), AutoPausedField]
        public TimeSpan NextDeletionTime = TimeSpan.Zero;

        public Vector2 OriginalScale = Vector2.Zero;

        public Vector2 AnimationScale = new Vector2(0.01f, 0.01f);

        public EntityUid Chasm;
    }
}
