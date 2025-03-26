using Content.Shared._Stories;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _rescache = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<XenoSkinChangeRSIEvent>(XenoSkinChangeRSI);
    }

    private void XenoSkinChangeRSI(XenoSkinChangeRSIEvent args, EntitySessionEventArgs session)
    {
        var xeno = GetEntity(args.NetEntity);
        if (!_entMan.TryGetComponent<SpriteComponent>(xeno, out var sprite))
            return;

        if (!_rescache.TryGetResource(args.SkinPath, out RSIResource? rsi))
            return;

        sprite.LayerSetRSI(0, rsi.RSI);
    }
}
