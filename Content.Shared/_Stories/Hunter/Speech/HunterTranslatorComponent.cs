using Content.Shared._Stories.Hunter.Profiles;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Speech;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
public sealed partial class HunterTranslatorComponent : Component
{
    [DataField] [AutoNetworkedField]
    public HunterSoundStyle Style = HunterSoundStyle.Modern;
}
