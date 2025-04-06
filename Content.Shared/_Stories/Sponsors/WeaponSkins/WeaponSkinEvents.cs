using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sponsors.WeaponSkins;

[Serializable, NetSerializable]
public sealed partial class WeaponSkinAppliedEvent : SimpleDoAfterEvent
{
    [DataField("applyingUser", required: true)]
    public NetEntity ApplyingUser;

    [DataField("targetEntity", required: true)]
    public NetEntity TargetEntity;

    [DataField("sprayCanEntity", required: true)]
    public NetEntity SprayCanEntity;

    [DataField("skinId", required: true)]
    public string SkinId;

    private WeaponSkinAppliedEvent()
    {
        SkinId = string.Empty;
    }

    public WeaponSkinAppliedEvent(NetEntity applyingUser, NetEntity targetEntity, NetEntity sprayCanEntity, string skinId)
    {
        ApplyingUser = applyingUser;
        TargetEntity = targetEntity;
        SprayCanEntity = sprayCanEntity;
        SkinId = skinId;
    }
}
