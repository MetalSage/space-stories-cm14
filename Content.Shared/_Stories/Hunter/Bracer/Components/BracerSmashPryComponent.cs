using Robust.Shared.Audio;

namespace Content.Shared._Stories.Hunter.Bracer.Components;

[RegisterComponent]
public sealed partial class BracerSmashPryComponent : Component
{
    [DataField]
    public SoundSpecifier SmashSound = new SoundPathSpecifier("/Audio/Effects/metal_crash.ogg");
}
