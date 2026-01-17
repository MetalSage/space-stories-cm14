using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components.SurgeryTools;

[RegisterComponent] [NetworkedComponent]
public sealed partial class HealingGunToolComponent : Component, ICMSurgeryToolComponent
{
    public string ToolName => "a healing gun";
}
