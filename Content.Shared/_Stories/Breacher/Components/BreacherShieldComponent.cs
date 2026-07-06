using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Breacher.Components;

/// <summary>
///     A worn/carried shield that has a chance to fully block incoming melee damage from the front.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BreacherShieldSystem))]
public sealed partial class BreacherShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PassiveBlockChance = 0.45f;

    [DataField, AutoNetworkedField]
    public float RaisedBlockChance = 0.8f;

    [DataField]
    public SoundSpecifier BlockSound = new SoundCollectionSpecifier("MetalThud");

    [DataField]
    public SoundSpecifier BashSound = new SoundCollectionSpecifier("MetalSlam");
}
