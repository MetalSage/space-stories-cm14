using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids.DeployTrap;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Popups;

namespace Content.Shared._RMC14.Xenonids.AcidInsight;

public sealed class RMCXenoAcidInsightSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCXenoDeployTrapsSystem _deployTraps = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCXenoAcidInsightComponent, XenoProjectileHitUserEvent>(OnProjectileHit);
    }

    public bool TryUseEmpower(Entity<RMCXenoAcidInsightComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        if (!ent.Comp.Empowered)
            return false;

        ent.Comp.Empowered = false;
        DirtyField(ent.Owner, ent.Comp, nameof(RMCXenoAcidInsightComponent.Empowered));
        return true;
    }

    private void OnProjectileHit(Entity<RMCXenoAcidInsightComponent> ent, ref XenoProjectileHitUserEvent args)
    {
        if (ent.Comp.Empowered || !HasComp<MarineComponent>(args.Hit))
            return;

        var stacks = 1;
        if (_deployTraps.IsTrapped(args.Hit))
            stacks = ent.Comp.MaxStacks;

        ent.Comp.Stacks += stacks;

        if (ent.Comp.Stacks >= ent.Comp.MaxStacks)
        {
            ent.Comp.Stacks = 0;
            Empower(ent);
        }

        DirtyField(ent.Owner, ent.Comp, nameof(RMCXenoAcidInsightComponent.Stacks));
    }

    private void Empower(Entity<RMCXenoAcidInsightComponent> ent)
    {
        ent.Comp.Empowered = true;
        DirtyField(ent.Owner, ent.Comp, nameof(RMCXenoAcidInsightComponent.Empowered));

        _popup.PopupPredicted(Loc.GetString(ent.Comp.EmpoweredMessage), ent, null, PopupType.SmallCaution);
    }
}
