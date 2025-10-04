using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleGunComponent : Component
{
	[DataField, AutoNetworkedField]
	public bool NeedHands;

    [DataField, AutoNetworkedField]
    public float DisableAtHullDamage = -1f;

    [DataField, AutoNetworkedField]
    public EntityUid? User;

    [ViewVariables]
    public ContainerSlot ActiveMagazineContainer = default!;

    [DataField, AutoNetworkedField]
    public string ActiveMagazineContainerId = "active-magazine";

    [ViewVariables]
    public Container SpareMagazinesContainer = default!;

    [DataField, AutoNetworkedField]
    public string SpareMagazinesContainerId = "spare-magazines";

    [DataField, AutoNetworkedField]
    public int MaxSpareMagazines = 3;

    [DataField(required: true), AutoNetworkedField]
    public List<string> AcceptedMagazineTypes = new();

    [DataField, AutoNetworkedField]
    public string? StartingMagazinePrototype;
}
