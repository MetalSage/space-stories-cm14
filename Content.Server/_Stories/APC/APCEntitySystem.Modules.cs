using Robust.Shared.Random;

namespace Content.Server._Stories.APC;

public sealed partial class APCEntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public void InitializeModules()
    {}
}
