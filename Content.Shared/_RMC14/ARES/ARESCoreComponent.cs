using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.ARES;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ARESCoreComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ARESCoreName = "Hermes";

    [DataField, AutoNetworkedField]
    public EntProtoId<IFFFactionComponent> Faction = "FactionMarine";

    [DataField, AutoNetworkedField]
    public bool Active = true;

    [DataField, AutoNetworkedField]
    public int MaxLogs = 5000;

    [DataField, AutoNetworkedField]
    public TimeSpan GasReleaseCooldown = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan NextGasRelease;

    [DataField, AutoNetworkedField]
    public bool LockdownActive;

    [DataField, AutoNetworkedField]
    public TimeSpan LockdownCooldown = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan NextLockdown;

    // Client Empty.
    [DataField, Access(typeof(ARESCoreSystem), Other = AccessPermissions.None)]
    public Dictionary<EntProtoId<ARESLogTypeComponent>, List<string>> Logs = new();
}
