using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Xenonids.AcidAnimation;
using Content.Shared.Actions.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Xenonids.AcidAnimation;

public sealed class XenoAcidAnimationSystem : SharedXenoAcidAnimationSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Dictionary<string, XenoAcidAnimationConfig> Configs = new()
    {
        ["CMXenoBoiler"] = new(
            new ResPath("_Stories/Mobs/Xenos/Boiler/boiler.rsi"),
            new Vector2(0f, 0.28125f),
            "ActionXenoAcidStrong",
            "ActionXenoBombard",
            "ActionXenoSprayAcidBoiler"),

        ["RMCXenoBoilerTrapper"] = new(
            new ResPath("_Stories/Mobs/Xenos/Boiler/trapper_boiler.rsi"),
            new Vector2(0f, 0.25f),
            "ActionXenoAcidStrong",
            "ActionXenoAcidShotgun"),

        ["CMXenoSpitter"] = new(
            new ResPath("_Stories/Mobs/Xenos/Spitter/spitter_spit.rsi"),
            new Vector2(0f, 0.1875f),
            "ActionXenoAcidNormal",
            "ActionXenoSpit",
            "ActionXenoSprayAcid"),

        ["CMXenoSentinel"] = new(
            new ResPath("_Stories/Mobs/Xenos/Sentinel/sentinel_spit.rsi"),
            new Vector2(0f, 0.1875f),
            "ActionXenoAcidWeak",
            "ActionXenoSlowingSpit",
            "ActionXenoScatteredSpit"),

        ["CMXenoPraetorian"] = new(
            new ResPath("_Stories/Mobs/Xenos/Praetorian/praetorian_spit.rsi"),
            new Vector2(0f, 0.125f),
            "ActionXenoAcidNormal",
            "ActionXenoSpitPraetorian",
            "ActionXenoAcidBall",
            "ActionXenoSprayAcidPraetorian"),

        ["CMXenoQueen"] = new(
            new ResPath("_Stories/Mobs/Xenos/Queen/queen_spit.rsi"),
            new Vector2(0f, 0.875f),
            "ActionXenoAcidNormal",
            "ActionXenoSpitQueen"),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoComponent, MapInitEvent>(OnXenoMapInit);

        SubscribeLocalEvent<XenoAcidAnimationComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<XenoAcidAnimationComponent, MindRemovedMessage>(OnMindRemoved);

        SubscribeNetworkEvent<XenoAcidAnimationToggleEvent>(OnToggle);
    }

    private void OnXenoMapInit(Entity<XenoComponent> ent, ref MapInitEvent args)
    {
        var protoId = MetaData(ent).EntityPrototype?.ID;
        if (protoId == null || !Configs.TryGetValue(protoId, out var config))
            return;

        var comp = EnsureComp<XenoAcidAnimationComponent>(ent);
        comp.SpitRsi = config.SpitRsi;
        comp.Offset = config.Offset;
        comp.ActionIds.Clear();
        comp.ActionIds.AddRange(config.ActionIds);
        Dirty(ent, comp);
    }

    private void OnDetached(Entity<XenoAcidAnimationComponent> ent, ref PlayerDetachedEvent args)
    {
        SetActive(ent, false);
    }

    private void OnMindRemoved(Entity<XenoAcidAnimationComponent> ent, ref MindRemovedMessage args)
    {
        SetActive(ent, false);
    }

    private void OnToggle(XenoAcidAnimationToggleEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var xeno = GetEntity(ev.Xeno);
        if (xeno != user || !TryComp<XenoAcidAnimationComponent>(xeno, out var comp))
            return;

        if (comp.Active == ev.Active)
            return;

        if (!CanToggle((xeno, comp), ev.Action, ev.Active))
            return;

        SetActive((xeno, comp), ev.Active);
    }

    private bool CanToggle(Entity<XenoAcidAnimationComponent> xeno, NetEntity netAction, bool active)
    {
        if (!active)
            return true;

        if (_timing.CurTime < xeno.Comp.NextToggleAt)
            return false;

        var action = GetEntity(netAction);
        if (!TryComp<ActionComponent>(action, out var actionComp) ||
            actionComp.AttachedEntity != xeno.Owner ||
            !IsAcidAnimationAction(action, xeno.Comp))
        {
            return false;
        }

        xeno.Comp.NextToggleAt = _timing.CurTime + TimeSpan.FromSeconds(xeno.Comp.ToggleRateLimit);
        return true;
    }

    private void SetActive(Entity<XenoAcidAnimationComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        Dirty(ent, ent.Comp);
    }

    private sealed class XenoAcidAnimationConfig
    {
        public readonly ResPath SpitRsi;
        public readonly Vector2 Offset;
        public readonly EntProtoId[] ActionIds;

        public XenoAcidAnimationConfig(ResPath spitRsi, Vector2 offset, params EntProtoId[] actionIds)
        {
            SpitRsi = spitRsi;
            Offset = offset;
            ActionIds = actionIds;
        }
    }
}
