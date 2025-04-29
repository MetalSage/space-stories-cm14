using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Content.Shared._Stories.APC;

[RegisterComponent]
public sealed partial class APCControllerComponent : Component
{
    [ViewVariables]
    public EntityUid? CurrentUser;

    [ViewVariables]
    public EntityUid? CurrentAPC;
}
