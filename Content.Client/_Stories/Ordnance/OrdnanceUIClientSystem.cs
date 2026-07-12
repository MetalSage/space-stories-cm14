using System;
using Content.Client._Stories.Ordnance.Simulator;
using Content.Client._Stories.Ordnance.Triggers;
using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Simulator;
using Content.Shared._Stories.Ordnance.Triggers;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Stories.Ordnance;

public sealed class OrdnanceUIClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrdnanceTimerComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<OrdnanceProxSensorComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<OrdnanceSignallerComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<DemolitionsSimulatorComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState<T>(Entity<T> ent, ref AfterAutoHandleStateEvent args) where T : IComponent
    {
        try
        {
            if (!TryComp<UserInterfaceComponent>(ent, out var ui))
                return;

            foreach (var bui in ui.ClientOpenInterfaces.Values)
            {
                if (bui is OrdnanceTimerBoundUserInterface timerUi) timerUi.Refresh();
                else if (bui is OrdnanceProxSensorBoundUserInterface proxUi) proxUi.Refresh();
                else if (bui is OrdnanceSignallerBoundUserInterface sigUi) sigUi.Refresh();
                else if (bui is DemolitionsSimulatorBoundUserInterface simUi) simUi.Refresh();
            }
        }
        catch (Exception e)
        {
            Log.Error($"[OrdnanceUIClientSystem] Ошибка обновления UI для {typeof(T).Name}: {e}");
        }
    }
}
