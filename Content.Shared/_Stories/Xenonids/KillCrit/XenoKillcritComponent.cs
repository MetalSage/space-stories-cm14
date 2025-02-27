using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.KillCrit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoKillcritSystem))]
public sealed partial class XenoKillcritComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new(); // Пустой слот заполняемый в прототипе для количества урона

    [DataField, AutoNetworkedField]
    public float Range = 1; // Сколько тайлов для активации абилки

    [DataField, AutoNetworkedField]
    public EntProtoId Effect = "ИЗМЕНИ ЭФФЕКТ!";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("ИЗВЛЕКИ ЗВУК!!!");// Звук при активации абилки

    [DataField, AutoNetworkedField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(5); // Не ебу че это
}
