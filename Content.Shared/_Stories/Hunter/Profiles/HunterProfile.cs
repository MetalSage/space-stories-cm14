using System.Linq;
using Content.Shared._Stories.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Profiles;

[Serializable] [NetSerializable] [DataDefinition]
public sealed partial class HunterProfile
{
    public HunterProfile() { }

    public HunterProfile(HunterProfile other)
    {
        Name = other.Name;
        Gender = other.Gender;
        Age = other.Age;
        FlavorText = other.FlavorText;
        Status = other.Status;
        SkinColor = other.SkinColor;
        QuillMarkingId = other.QuillMarkingId;
        ArmorPrototype = other.ArmorPrototype;
        MaskPrototype = other.MaskPrototype;
        GreavesPrototype = other.GreavesPrototype;
        CasterPrototype = other.CasterPrototype;
        Voice = other.Voice;
        HeadAccessory = other.HeadAccessory;
        TranslatorSound = other.TranslatorSound;
        CloakSound = other.CloakSound;
        CapeColor = other.CapeColor;
        BracerPrototype = other.BracerPrototype;
    }

    [DataField("name")]
    public string Name { get; set; } = "Yautja";

    [DataField("gender")]
    public Gender Gender { get; set; } = Gender.Male;

    [DataField("age")]
    public int Age { get; set; } = 175;

    [DataField("flavorText")]
    public string FlavorText { get; set; } = "";

    [DataField("status")]
    public HunterStatus Status { get; set; } = HunterStatus.Normal;

    [DataField("skinColor")]
    public Color SkinColor { get; set; } = Color.White;

    [DataField("quillMarkingId")]
    public ProtoId<MarkingPrototype> QuillMarkingId { get; set; } = "HunterHairStandard";

    [DataField("armorPrototype")]
    public EntProtoId ArmorPrototype { get; set; } = "STHalfArmorClanHunterEbony1";

    [DataField("maskPrototype")]
    public EntProtoId MaskPrototype { get; set; } = "STMaskHunterEbony1";

    [DataField("greavesPrototype")]
    public EntProtoId GreavesPrototype { get; set; } = "STBootsHunterEbony1";

    [DataField("casterPrototype")]
    public EntProtoId CasterPrototype { get; set; } = "STBracerAttachmentPlasmaCasterEbony";

    [DataField("voice")]
    public string Voice { get; set; } = "STHunter";

    [DataField("headAccessory")]
    public EntProtoId HeadAccessory { get; set; } = "Nothing";

    [DataField("translatorSound")]
    public HunterSoundStyle TranslatorSound { get; set; } = HunterSoundStyle.Modern;

    [DataField("cloakSound")]
    public HunterSoundStyle CloakSound { get; set; } = HunterSoundStyle.Modern;

    [DataField("capeColor")]
    public Color CapeColor { get; set; } = Color.FromHex("#FFFFFF");

    [DataField("bracerPrototype")]
    public EntProtoId BracerPrototype { get; set; } = "STBracerHunter";

    public HunterProfile Clone()
    {
        return new HunterProfile(this);
    }

    public static HunterProfile Random(IPrototypeManager prototypeManager, IRobustRandom random)
    {
        var profile = new HunterProfile();

        profile.Gender = random.Pick(new[] { Gender.Male, Gender.Female });
        profile.Age = random.Next(175, 2500);

        profile.Name = profile.Gender == Gender.Male ? "Yautja Male" : "Yautja Female";

        EntProtoId PickProto(string prefix, string fallback = "")
        {
            var protos = prototypeManager
                .EnumeratePrototypes<EntityPrototype>()
                .Where(p => !p.Abstract && p.ID.StartsWith(prefix))
                .Select(p => (EntProtoId)p.ID)
                .ToList();
            if (protos.Count > 0)
                return random.Pick(protos);
            return !string.IsNullOrEmpty(fallback) ? (EntProtoId)fallback : default;
        }

        var armor = PickProto("STHalfArmor");
        if (!string.IsNullOrEmpty(armor))
            profile.ArmorPrototype = armor;

        var mask = PickProto("STMaskHunter");
        if (string.IsNullOrEmpty(mask))
            mask = PickProto("STHelmetHunter");
        if (!string.IsNullOrEmpty(mask))
            profile.MaskPrototype = mask;

        var greaves = PickProto("STBootsHunter");
        if (string.IsNullOrEmpty(greaves))
            greaves = PickProto("STGreavesHunter");
        if (!string.IsNullOrEmpty(greaves))
            profile.GreavesPrototype = greaves;

        var caster = PickProto("STBracerAttachmentPlasmaCaster");
        if (!string.IsNullOrEmpty(caster))
            profile.CasterPrototype = caster;

        var accessory = PickProto("STHeadAccessory");
        if (!string.IsNullOrEmpty(accessory))
            profile.HeadAccessory = accessory;

        var bracer = PickProto("STBracerHunter");
        if (!string.IsNullOrEmpty(bracer))
            profile.BracerPrototype = bracer;

        var sex = profile.Gender == Gender.Male ? Sex.Male : Sex.Female;
        var voices = prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(v => v.RoundStart && (v.Sex == sex || v.Sex == Sex.Unsexed))
            .ToList();

        if (voices.Count > 0)
            profile.Voice = random.Pick(voices).ID;

        profile.SkinColor = new Color(
            random.NextFloat(0.5f, 1f),
            random.NextFloat(0.5f, 1f),
            random.NextFloat(0.4f, 0.9f)
        );
        profile.CapeColor = new Color(random.NextFloat(), random.NextFloat(), random.NextFloat());

        profile.TranslatorSound = random.Pick(Enum.GetValues<HunterSoundStyle>());
        profile.CloakSound = random.Pick(Enum.GetValues<HunterSoundStyle>());

        return profile;
    }
}

[Serializable] [NetSerializable]
public enum HunterStatus
{
    Normal,
    Council,
    Leader,
}

[Serializable] [NetSerializable]
public enum HunterSoundStyle
{
    Modern,
    Retro,
}
