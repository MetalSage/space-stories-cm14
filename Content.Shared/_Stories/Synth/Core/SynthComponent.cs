using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.StatusIcon;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Whitelist;

namespace Content.Shared._Stories.Synth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSynthSystem), typeof(SharedSynthGenerationSystem))]
public sealed partial class SynthComponent : Component
{
    [DataField]
    public EntProtoId AddComponents = "STSynthAddComponents";

    [DataField]
    public EntProtoId RemoveComponents = "STSynthRemoveComponents";

    [DataField, AutoNetworkedField]
    public float? StunResistance = 2.5f;

    [DataField, AutoNetworkedField]
    public bool CanUseGuns = false;

    [DataField, AutoNetworkedField]
    public bool CanUseMeleeWeapons = true;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> NewBloodReagent = "STSynthBlood";

    [DataField, AutoNetworkedField]
    public ProtoId<DamageModifierSetPrototype> NewDamageModifier = "STSynth";

    [DataField, AutoNetworkedField]
    public LocId SpeciesName = "st-species-name-synth";

    [DataField, AutoNetworkedField]
    public LocId FixedIdentityReplacement = "cm-chatsan-replacement-synth";

    [DataField, AutoNetworkedField]
    public Dictionary<RMCHealthIconTypes, ProtoId<HealthIconPrototype>> HealthIconOverrides = new()
    {
        [RMCHealthIconTypes.Healthy] = "STHealthIconHealthySynth",
        [RMCHealthIconTypes.DeadDefib] = "STHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadClose] = "STHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadAlmost] = "STHealthIconDeadSynth",
        [RMCHealthIconTypes.DeadDNR] = "STHealthIconDeadDNRSynth",
        [RMCHealthIconTypes.Dead] = "STHealthIconDeadSynth",
        [RMCHealthIconTypes.HCDead] = "STHealthIconDeadSynth",
    };

    [DataField, AutoNetworkedField]
    public EntProtoId<OrganComponent> NewBrain = "STOrganSynthBrain";

    [DataField, AutoNetworkedField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(0);

    [DataField, AutoNetworkedField]
    public TimeSpan SelfRepairTime = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public FixedPoint2 CritThreshold = FixedPoint2.New(199);

    [DataField, AutoNetworkedField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Welding";

    [DataField]
    public DamageSpecifier? WelderDamageToRepair = new()
    {
        DamageDict = {
            ["Blunt"] = -15,
            ["Piercing"] = -15,
            ["Slash"] = -15,
        },
    };

    [DataField]
    public DamageSpecifier? CableCoilDamageToRepair = new()
    {
        DamageDict = {
            ["Caustic"] = -15,
            ["Heat"] = -15,
            ["Shock"] = -15,
            ["Cold"] = -15,
        },
    };

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> WelderDamageGroup = "Brute";

    [DataField, AutoNetworkedField]
    public ProtoId<DamageGroupPrototype> CableCoilDamageGroup = "Burn";

    [DataField, AutoNetworkedField]
    public string DamageVisualsColor = "#EEEEEE";

    [DataField]
    public TimeSpan NextUnableUsePopup;

}

