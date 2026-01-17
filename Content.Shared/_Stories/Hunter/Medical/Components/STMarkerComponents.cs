using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components;

[RegisterComponent] [NetworkedComponent]
public sealed partial class STWoundsStabilizedComponent : Component;

[RegisterComponent] [NetworkedComponent]
public sealed partial class STWoundsMendedComponent : Component;

[RegisterComponent] [NetworkedComponent]
public sealed partial class STSurgeryDamagedConditionComponent : Component;
