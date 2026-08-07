using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ChasmTeleporter;

[NetworkedComponent, RegisterComponent, Access(typeof(ChasmTeleporterSystem))]
public sealed partial class ChasmTeleporterTargetComponent : Component
{
}
