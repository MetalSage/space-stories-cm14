using System.Numerics;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._Stories.Xenonids.Summon;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Dataset;
using Content.Shared.Pointing;
using Robust.Shared.Map;


namespace Content.Server.Summoner
{
    /// <inheritdoc/>
    public sealed class SummonerSystem : SharedSummonerSystem
    {
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly HTNSystem _htn = default!;
        [Dependency] private readonly NPCSystem _npc = default!;
        [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedXenoHiveSystem _xenoHive = default!;
        

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SummonerComponent, SummonerRaiseArmyActionEvent>(OnRaiseArmy);
            SubscribeLocalEvent<SummonerComponent, AfterPointedAtEvent>(OnPointedAt);
        }

        /// <summary>
        /// Summons an allied rat servant at the King, costing a small amount of hunger
        /// </summary>
        private void OnRaiseArmy(EntityUid uid, SummonerComponent component, SummonerRaiseArmyActionEvent args)
        {
            if (args.Handled)
                return;

            if (!_xenoPlasma.TryRemovePlasma(uid, component.PlasmaCost))
                return;


            args.Handled = true;
            for (int i = 0; i < 5; i++)
            {
                var servant = Spawn(component.ArmyMobSpawnId, Transform(uid).Coordinates);
                _xenoHive.SetSameHive(uid, servant);
                var comp = EnsureComp<SummonerServantComponent>(servant);
                comp.King = uid;
                Dirty(servant, comp);

                component.Servants.Add(servant);
                _npc.SetBlackboard(servant, NPCBlackboard.FollowTarget, new EntityCoordinates(uid, Vector2.Zero));
                UpdateServantNpc(servant, component.CurrentOrder);
            }
        }


        private void OnPointedAt(EntityUid uid, SummonerComponent component, ref AfterPointedAtEvent args)
        {
            if (component.CurrentOrder != SummonerOrderType.Attack)
                return;

            foreach (var servant in component.Servants)
            {
                _npc.SetBlackboard(servant, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
            }
        }

        public override void UpdateServantNpc(EntityUid uid, SummonerOrderType orderType)
        {
            base.UpdateServantNpc(uid, orderType);

            if (!TryComp<HTNComponent>(uid, out var htn))
                return;

            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, orderType);
            _htn.Replan(htn);
        }

    }
}
