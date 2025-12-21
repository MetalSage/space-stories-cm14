using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.OrbitalCannon;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Stories.Nuke;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STNukeComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan DecryptionTime = TimeSpan.FromMinutes(10);

    [DataField, AutoNetworkedField]
    public TimeSpan DetonationTime = TimeSpan.FromMinutes(3);

    [DataField, AutoNetworkedField]
    public TimeSpan PenaltionTime = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan? DecryptionOn;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExplodeOn;

    [DataField, AutoNetworkedField]
    public bool Safety = true;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public bool Decryption;

    [DataField, AutoNetworkedField]
    public bool DecryptionComplete;

    [DataField, AutoNetworkedField]
    public int RequiredTowers = 2;

    [DataField, AutoNetworkedField]
    public List<EntityUid> LinkedTowers = new();

    [DataField, AutoNetworkedField]
    public bool CommandLockout;

    [DataField, AutoNetworkedField]
    public bool AnnouncedHalfway;

    [DataField, AutoNetworkedField]
    public bool AnnouncedOneMinute;

    [DataField]
    public TimeSpan? LastTowerCheck;

    [DataField]
    public TimeSpan TowerCheckInterval = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public bool TowersWereOffline;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> DefuseSkill;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExplodeStage1At;

    [DataField, AutoNetworkedField]
    public TimeSpan? ExplodeStage2At;

    [DataField, AutoNetworkedField]
    public bool ExplodeSoundPlayed;

    [DataField, AutoNetworkedField]
    public bool Nuked;

    [DataField, AutoNetworkedField]
    public bool Exploded;

    [DataField, AutoNetworkedField]
    public EntProtoId<OrbitalCannonExplosionComponent> Explosion = "STNukeExplosionExplosive";

    [DataField, AutoNetworkedField]
    public SoundSpecifier BeforeNukeSound = new SoundPathSpecifier("/Audio/_Stories/Nuke/nuke.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier NukeSound = new SoundCollectionSpecifier("STNukeSoundCollection", AudioParams.Default.WithVolume(100));
}
