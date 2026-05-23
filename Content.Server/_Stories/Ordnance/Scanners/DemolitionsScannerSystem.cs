using System;
using System.Collections.Generic;
using System.Text;
using Content.Server._Stories.Ordnance.Explosion;
using Content.Server.Paper;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._Stories.Ordnance;
using Content.Shared._Stories.Ordnance.Scanners;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Ordnance.Scanners;

public sealed class DemolitionsScannerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly OrdnanceExplosionSystem _ordnanceExplosion = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private readonly List<ReagentQuantity> _reagentCache = new();
    private readonly List<Entity<OrdnanceCasingComponent>> _casingsCache = new();

    private static readonly string SkillEngineer = "RMCSkillEngineer";
    private static readonly EntProtoId PaperProto = "Paper";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemolitionsScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DemolitionsScannerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void FindContentsRecursive(EntityUid uid)
    {
        if (TryComp<SolutionContainerManagerComponent>(uid, out var solMan))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((uid, solMan)))
            {
                _reagentCache.AddRange(solution.Comp.Solution.Contents);
            }
        }

        if (TryComp<OrdnanceCasingComponent>(uid, out var casing))
        {
            _casingsCache.Add((uid, casing));
        }

        if (TryComp<ContainerManagerComponent>(uid, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    FindContentsRecursive(ent);
                }
            }
        }
    }

    private void OnUseInHand(Entity<DemolitionsScannerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;

        if (string.IsNullOrEmpty(ent.Comp.LastScanText))
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-scanner-no-scan"), ent, args.User);
            return;
        }

        var paper = Spawn(PaperProto, Transform(args.User).Coordinates);
        _metaData.SetEntityName(paper, ent.Comp.LastScanName ?? "Scan");

        if (TryComp<PaperComponent>(paper, out var paperComp))
        {
            _paper.SetContent((paper, paperComp), ent.Comp.LastScanText);
        }

        _hands.TryPickupAnyHand(args.User, paper);
        _popup.PopupEntity(Loc.GetString("stories-demo-scanner-print-success"), ent, args.User);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/twobeep.ogg"), ent, AudioParams.Default.WithVolume(-4f));
        args.Handled = true;
    }

    private void OnAfterInteract(Entity<DemolitionsScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!_skills.HasSkill(args.User, SkillEngineer, 1))
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-scanner-no-skill", ("scanner", Name(ent.Owner))), target, args.User);
            return;
        }

        var sb = new StringBuilder();

        if (TryComp<RMCFlamerTankComponent>(target, out var flamerTank))
        {
            if (!_solutions.TryGetSolution(target, flamerTank.SolutionId, out var tankSolEnt, out _) || tankSolEnt.Value.Comp.Solution.Contents.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("stories-demo-scanner-flamer-empty", ("target", Name(target))), target, args.User);
                return;
            }

            var firstReagent = tankSolEnt.Value.Comp.Solution.Contents[0].Reagent.Prototype;
            if (_reagents.TryIndex(firstReagent, out var reagentProto))
            {
                var intensity = MathF.Round(Math.Min(reagentProto.Intensity, flamerTank.MaxIntensity));
                var duration = MathF.Round(Math.Min(reagentProto.Duration, flamerTank.MaxDuration));
                var range = MathF.Round(Math.Min(reagentProto.Radius, flamerTank.MaxRange));

                sb.AppendLine(Loc.GetString("stories-demo-scanner-flamer-stats-header"));
                sb.AppendLine(Loc.GetString("stories-demo-scanner-flamer-stats-intensity", ("intensity", intensity)));
                sb.AppendLine(Loc.GetString("stories-demo-scanner-flamer-stats-duration", ("duration", duration)));
                sb.AppendLine(Loc.GetString("stories-demo-scanner-flamer-stats-range", ("range", range)));

                var text = sb.ToString().TrimEnd();
                _popup.PopupEntity(text, target, args.User);
                ent.Comp.LastScanName = Name(target);
                ent.Comp.LastScanText = text;
                Dirty(ent);
                return;
            }
        }

        _reagentCache.Clear();
        _casingsCache.Clear();

        FindContentsRecursive(target);

        if (_reagentCache.Count == 0 && _casingsCache.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-scanner-empty"), target, args.User);
            return;
        }

        var reagentTotals = new Dictionary<string, FixedPoint2>();
        foreach (var req in _reagentCache)
        {
            if (reagentTotals.ContainsKey(req.Reagent.Prototype))
                reagentTotals[req.Reagent.Prototype] += req.Quantity;
            else
                reagentTotals[req.Reagent.Prototype] = req.Quantity;
        }

        var chemSb = new StringBuilder();
        foreach (var (proto, qty) in reagentTotals)
        {
            var name = proto;
            if (_reagents.TryIndex(proto, out var rProto))
                name = Loc.GetString(rProto.LocalizedName);
            chemSb.AppendLine($"- {name}: {MathF.Round(qty.Float())}u");
        }

        if (reagentTotals.Count > 0)
        {
            sb.AppendLine(Loc.GetString("stories-demo-scanner-report-chemicals-header"));
            sb.Append(chemSb.ToString());
        }

        var foundHazard = false;

        if (_casingsCache.Count > 0)
        {
            foreach (var casingEnt in _casingsCache)
            {
                var casingReagents = new List<ReagentQuantity>();
                _ordnanceExplosion.PopulateContentsCache(casingEnt.Owner, casingEnt.Comp);
                var effCasing = _ordnanceExplosion.GetEffectiveCasing(casingEnt.Owner, casingEnt.Comp);
                var stats = _ordnanceExplosion.CalculateExplosionStats(casingReagents, casingEnt.Comp, effCasing);

                if (stats.Power > 0 || stats.FireIntensity > 0)
                {
                    foundHazard = true;
                    if (stats.Power > 0)
                    {
                        sb.AppendLine(Loc.GetString("stories-demo-scanner-report-explosive"));
                        sb.AppendLine(Loc.GetString("stories-demo-scanner-report-explosive-power", ("power", MathF.Round(stats.Power))));
                    }
                    if (stats.FireIntensity > 0)
                    {
                        sb.AppendLine(Loc.GetString("stories-demo-scanner-report-fire"));
                        sb.AppendLine(Loc.GetString("stories-demo-scanner-report-fire-stats", ("intensity", MathF.Round(stats.FireIntensity)), ("radius", MathF.Round(stats.FireRadius))));
                    }
                }
            }
        }
        else
        {
            var stats = _ordnanceExplosion.CalculateExplosionStats(_reagentCache, null, null);
            if (stats.Power > 0 || stats.FireIntensity > 0)
            {
                foundHazard = true;
                if (stats.Power > 0)
                {
                    sb.AppendLine(Loc.GetString("stories-demo-scanner-report-explosive"));
                    sb.AppendLine(Loc.GetString("stories-demo-scanner-report-explosive-power", ("power", MathF.Round(stats.Power))));
                }
                if (stats.FireIntensity > 0)
                {
                    sb.AppendLine(Loc.GetString("stories-demo-scanner-report-fire"));
                    sb.AppendLine(Loc.GetString("stories-demo-scanner-report-fire-stats", ("intensity", MathF.Round(stats.FireIntensity)), ("radius", MathF.Round(stats.FireRadius))));
                }
            }
        }

        if (!foundHazard && reagentTotals.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("stories-demo-scanner-empty"), target, args.User);
            return;
        }

        var finalMsg = sb.ToString().TrimEnd();
        _popup.PopupEntity(finalMsg, target, args.User);

        ent.Comp.LastScanName = Name(target);
        ent.Comp.LastScanText = finalMsg;
        Dirty(ent);
    }
}
