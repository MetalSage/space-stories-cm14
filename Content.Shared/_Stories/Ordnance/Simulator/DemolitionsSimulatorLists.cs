namespace Content.Shared._Stories.Ordnance.Simulator;

public static class DemolitionsSimulatorLists
{
    public static readonly IReadOnlyList<string> Humans = new[]
    {
        "RMCRandomHumanoidUnassignedRifleman",
        "RMCRandomHumanoidCLFSoldier",
        "RMCRandomHumanoidSPPRiflemanHostile",
        "RMCRandomHumanoidPMCStandardSSG45",
        "RMCRandomHumanoidMarineRaider",
    };

    public static readonly IReadOnlyList<string> Xenomorphs = new[]
    {
        "CMXenoLesserDrone",
        "CMXenoDrone",
        "CMXenoRunner",
        "CMXenoDefender",
        "CMXenoSentinel",
        "CMXenoHivelord",
        "CMXenoCarrier",
        "CMXenoBurrower",
        "CMXenoLurker",
        "CMXenoWarrior",
        "CMXenoSpitter",
        "CMXenoRavager",
        "RMCXenoCrusher",
        "CMXenoPraetorian",
        "CMXenoBoiler",
        "CMXenoQueen",
        "RMCXenoKing",
    };

    public static readonly IReadOnlyList<string> Structures = new[]
    {
        "CMWallMetal",
        "CMWindowWhiteColony",
        "CMWallReinforced",
        "CMWindowWhiteColonyReinforced",
        "WallXenoResin",
        "WallXenoResinThick"
    };
}
