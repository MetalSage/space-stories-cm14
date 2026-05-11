using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Clothing;

[RegisterComponent, NetworkedComponent]
public sealed partial class HelmetTapComponent : Component
{
    [DataField]
    public SoundSpecifier TapSound = new SoundPathSpecifier("/Audio/_Stories/Effects/magazine_tap.ogg");

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan LastTapTime;
}