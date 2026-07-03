using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Emplacements;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StoriesFoldingBarricadeMountSystem))]
public sealed partial class StoriesFoldingBarricadeMountableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? TargetPrototype;

    [DataField, AutoNetworkedField]
    public EntProtoId? RevertPrototype;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? WeaponWhitelist;

    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";

    [DataField, AutoNetworkedField]
    public int RequiredSkillLevel = 1;

    [DataField, AutoNetworkedField]
    public TimeSpan MaxDelay = TimeSpan.FromSeconds(8);

    [DataField, AutoNetworkedField]
    public TimeSpan MinDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public int MaxSkillLevel = 4;

    [DataField]
    public SoundSpecifier? InstallSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");

    [DataField]
    public SoundSpecifier? UninstallSound = new SoundPathSpecifier("/Audio/Items/crowbar.ogg");
}
