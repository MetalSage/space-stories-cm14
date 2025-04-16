using Content.Shared._Stories.Sponsors.XenoSkins;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;

namespace Content.Client._Stories.Sponsors.XenoSkins;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _rescache = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<XenoSkinChangeRSIEvent>(XenoSkinChangeRSI);
    }

    private void XenoSkinChangeRSI(XenoSkinChangeRSIEvent args, EntitySessionEventArgs session)
    {
        if (!_entMan.TryGetComponent<SpriteComponent>(GetEntity(args.NetEntity), out var sprite))
            return;

        if (!_rescache.TryGetResource(args.SkinPath, out RSIResource? rsi))
            return;

        sprite.LayerSetRSI(0, rsi.RSI);
    }
}
