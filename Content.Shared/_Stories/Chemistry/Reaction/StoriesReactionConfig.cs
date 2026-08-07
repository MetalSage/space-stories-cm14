using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Chemistry.Reaction;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class StoriesReactionConfig
{
    [DataField]
    public float BubblingProbability = 0.2f;

    [DataField]
    public float BubblingSplashRadius = 1.5f;

    [DataField]
    public float BubblingSplashScale = 0.2f;

    [DataField]
    public LocId BubblingPopup = "stories-reaction-bubbling";

    [DataField]
    public float GlowingProbability = 0.5f;

    [DataField]
    public LocId GlowingPopup = "stories-reaction-glowing";

    [DataField]
    public float SmokingDelay = 4f;

    [DataField]
    public EntProtoId SmokeEntity = "STTransparentSmoke";

    [DataField]
    public float SmokeVolumeScale = 0.1f;

    [DataField]
    public float SmokeSpreadDivisor = 10f;

    [DataField]
    public LocId SmokingStartPopup = "stories-reaction-smoking-start";

    [DataField]
    public LocId SmokingPreventedPopup = "stories-reaction-smoking-prevented";

    [DataField]
    public SoundSpecifier SmokingStartSound = new SoundPathSpecifier("/Audio/_Stories/Effects/tankhiss3.ogg");

    [DataField]
    public SoundSpecifier SmokeSound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg");

    [DataField]
    public float FireDelay = 3f;

    [DataField]
    public EntProtoId FireEntity = "RMCTileFire";

    [DataField]
    public int FireRadius = 2;

    [DataField]
    public int FireIntensity = 15;

    [DataField]
    public int FireDuration = 10;

    [DataField]
    public LocId FireStartPopup = "stories-reaction-smoking-start";

    [DataField]
    public SoundSpecifier FireStartSound = new SoundPathSpecifier("/Audio/_Stories/Effects/tankhiss3.ogg");

    [DataField]
    public float EndothermicTempDrop = 50f;

    [DataField]
    public float EndothermicProbability = 0.15f;

    [DataField]
    public LocId EndothermicPopup = "stories-reaction-endothermic";
}
