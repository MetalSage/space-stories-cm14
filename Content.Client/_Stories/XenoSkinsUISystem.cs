using Content.Shared._Stories;

namespace Content.Client._Stories;

public sealed class XenoSkinsUISystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSkinsComponent, AfterAutoHandleStateEvent>(OnXenoSkinsAfterState);
    }

    private void OnXenoSkinsAfterState(Entity<XenoSkinsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(ent, out UserInterfaceComponent? ui))
            return;

        foreach (var bui in ui.ClientOpenInterfaces.Values)
        {
            // if (bui is XenoSkinsBui skinsBui)
            //     // skinsBui.Refresh();
        }
    }
}
