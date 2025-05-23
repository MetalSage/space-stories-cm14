using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Content.Shared._Stories.APC;

[RegisterComponent]
public sealed partial class APCDoorComponent : Component
{
    [DataField]
    public APCEnterSide Side;

    [DataField]
    public float LeaveDelay = 2f;
}
