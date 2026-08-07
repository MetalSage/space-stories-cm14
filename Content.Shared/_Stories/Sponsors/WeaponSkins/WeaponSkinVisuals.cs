using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sponsors.WeaponSkins;

[Serializable, NetSerializable]
public enum WeaponSkinVisuals : byte
{
    /// <summary>
    /// Key for AppearanceData storing the current Skin ID (string).
    /// </summary>
    Skin
}
