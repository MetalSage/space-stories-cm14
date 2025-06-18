using Content.Shared._Stories.Sponsors.XenoSkins;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._Stories.Sponsors.XenoSkins;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<XenoSkinChangeRSIEvent>(OnXenoSkinChangeRSI);
        SubscribeLocalEvent<XenoSkinsComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<XenoSkinsComponent> ent, ref ComponentStartup args)
    {
        ApplySkinFromComponentState(ent.Owner, ent.Comp);
    }

    private void OnXenoSkinChangeRSI(XenoSkinChangeRSIEvent args, EntitySessionEventArgs session)
    {
        var entity = GetEntity(args.NetEntity);
        if (!_entMan.TryGetComponent<SpriteComponent>(entity, out var sprite))
            return;

        if (!_resCache.TryGetResource(args.SkinPath, out RSIResource? rsi))
        {
            Log.Warning($"Unable to load RSI resource: {args.SkinPath} for entity {ToPrettyString(entity)} from network event.");
            return;
        }

        sprite.LayerSetRSI(0, rsi.RSI);
    }

    private void ApplySkinFromComponentState(EntityUid uid, XenoSkinsComponent xenoSkinsComp)
    {
        if (xenoSkinsComp.CurrentSkin == null)
            return;

        if (!_prototypeManager.TryIndex(xenoSkinsComp.CurrentSkin.Value, out var skinProto))
        {
            Log.Warning($"Unable to find XenoSkinsPrototype: {xenoSkinsComp.CurrentSkin.Value} for entity {ToPrettyString(uid)}");
            return;
        }

        if (!_entMan.TryGetComponent<SpriteComponent>(uid, out var spriteComp))
        {
            return;
        }

        var skinRsiPath = SpriteSpecifierSerializer.TextureRoot / skinProto.SpriteRsi;

        if (!_resCache.TryGetResource(skinRsiPath, out RSIResource? rsiResource))
        {
            Log.Warning($"Unable to load RSI resource: {skinRsiPath} for skin {skinProto.ID} on entity {ToPrettyString(uid)}");
            return;
        }

        spriteComp.LayerSetRSI(0, rsiResource.RSI);
    }
}
