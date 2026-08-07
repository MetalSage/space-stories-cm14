using Content.Server.Humanoid;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Chat;
using Content.Shared._Stories.Hunter.Bracer;
using Content.Shared._Stories.Hunter.Bracer.Components;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Profiles;
using Content.Shared._Stories.Hunter.Speech;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Hunter.Spawning;

public sealed class HunterSpawningSystem : EntitySystem
{
    [Dependency] private readonly BracerSystem _bracer = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly JobSystem _jobSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public EntityUid SpawnHunter(EntityCoordinates coordinates, HunterProfile profile, ICommonSession player)
    {
        var mob = Spawn("STMobHunter", coordinates);

        var mind = _mindSystem.GetMind(player.UserId);
        if (mind == null)
        {
            var mindEnt = _mindSystem.CreateMind(player.UserId, profile.Name);
            mind = mindEnt.Owner;
        }

        _mindSystem.TransferTo(mind.Value, mob);

        var humanoidProfile = ProfileToHumanoid(profile);
        _humanoidAppearance.LoadProfile(mob, humanoidProfile);
        _metaData.SetEntityName(mob, humanoidProfile.Name);

        var hunterComp = EnsureComp<HunterComponent>(mob);
        hunterComp.Status = profile.Status;
        Dirty(mob, hunterComp);

        if (humanoidProfile.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
        {
            var detail = EnsureComp<DetailExaminableComponent>(mob);
            detail.Content = humanoidProfile.FlavorText;
        }

        var jobId = "STHunter";
        var jobTitle = Loc.GetString("job-name-hunter-normal");
        var accessTag = "STAccessHunterSecure";

        switch (profile.Status)
        {
            case HunterStatus.Council:
                jobId = "STHunterCouncil";
                jobTitle = Loc.GetString("job-name-hunter-council");
                accessTag = "STAccessHunterElder";
                break;
            case HunterStatus.Leader:
                jobId = "STHunterLeader";
                jobTitle = Loc.GetString("job-name-hunter-leader");
                accessTag = "STAccessHunterAncient";
                break;
        }

        _jobSystem.MindAddJob(mind.Value, jobId);

        var bracer = Spawn(profile.BracerPrototype, coordinates);

        if (TryComp<HunterBracerComponent>(bracer, out var bracerComp))
        {
            if (TryComp<AccessComponent>(bracer, out var access))
            {
                access.Tags.Add(accessTag);
                if (profile.Status >= HunterStatus.Council)
                    access.Tags.Add("STAccessHunterElite");
            }

            if (TryComp<IdCardComponent>(bracer, out var idCard))
            {
                idCard.JobTitle = jobTitle;
                idCard.FullName = profile.Name;
            }

            if (profile.CloakSound == HunterSoundStyle.Retro)
            {
                bracerComp.CloakOnSound = new SoundPathSpecifier("/Audio/_Stories/Effects/Hunter/pred_cloakon.ogg");
                bracerComp.CloakOffSound = new SoundPathSpecifier(
                    "/Audio/_Stories/Effects/Hunter/pred_cloakoff.ogg"
                );
            }
            else
            {
                bracerComp.CloakOnSound = new SoundPathSpecifier(
                    "/Audio/_Stories/Effects/Hunter/pred_cloakon_modern.ogg"
                );
                bracerComp.CloakOffSound = new SoundPathSpecifier(
                    "/Audio/_Stories/Effects/Hunter/pred_cloakoff_modern.ogg"
                );
            }
        }

        _inventory.TryEquip(mob, bracer, "gloves", true, true);

        if (
            !string.IsNullOrEmpty(profile.CasterPrototype)
            && _prototypeManager.HasIndex<EntityPrototype>(profile.CasterPrototype)
        )
        {
            if (_bracer.IsHunterWithBracer(mob, out var bracerEnt))
            {
                if (_itemSlots.TryGetSlot(bracerEnt.Value, BracerSystem.PlasmaCasterSlotId, out var slot))
                {
                    var caster = Spawn(profile.CasterPrototype, coordinates);
                    _itemSlots.TryInsert(bracerEnt.Value, slot, caster, null);
                }
            }
        }

        var headset = Spawn("STHeadsetHunterOverseer", coordinates);
        _inventory.TryEquip(mob, headset, "ears", true, true);

        var translator = EnsureComp<HunterTranslatorComponent>(mob);
        translator.Style = profile.TranslatorSound;
        Dirty(mob, translator);

        return mob;
    }

    private HumanoidCharacterProfile ProfileToHumanoid(HunterProfile profile)
    {
        var humanoidProfile = new HumanoidCharacterProfile();
        humanoidProfile = humanoidProfile.WithName(profile.Name);
        humanoidProfile = humanoidProfile.WithFlavorText(profile.FlavorText);
        humanoidProfile = humanoidProfile.WithAge(profile.Age);
        humanoidProfile = humanoidProfile.WithSex(profile.Gender == Gender.Male ? Sex.Male : Sex.Female);
        humanoidProfile = humanoidProfile.WithGender(profile.Gender);
        humanoidProfile = humanoidProfile.WithSpecies("STHunter");
        humanoidProfile = humanoidProfile.WithVoice(profile.Voice);

        var appearance = HumanoidCharacterAppearance.DefaultWithSpecies("STHunter");
        appearance = appearance.WithSkinColor(profile.SkinColor);

        var quillMarking = profile.QuillMarkingId;
        if (!_prototypeManager.HasIndex(quillMarking))
            quillMarking = "HunterHairStandard";

        appearance = appearance.WithMarkings(
            new List<Shared.Humanoid.Markings.Marking>
            {
                new(quillMarking, new List<Color> { profile.SkinColor }),
            }
        );

        humanoidProfile = humanoidProfile.WithCharacterAppearance(appearance);
        return humanoidProfile;
    }
}
