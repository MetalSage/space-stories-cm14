using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.ScissorsCut;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoScissorsCutSystem))]
public sealed partial class XenoScissorsCutActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan MissCooldown = TimeSpan.FromSeconds(1);
}
