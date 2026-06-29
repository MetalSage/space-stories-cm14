using System.Diagnostics.CodeAnalysis;
using Content.Shared._Stories.Sponsors;
using Robust.Client.Player;

namespace Content.Client._Stories.Sponsors;

public sealed class SponsorsSystem : EntitySystem
{
    [Dependency] private readonly SponsorsManager _manager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SponsorInfoUpdatedEvent>(OnSponsorInfoUpdated);
    }

    private void OnSponsorInfoUpdated(SponsorInfoUpdatedEvent ev)
    {
        _manager.SetSponsorInfo(ev.Info);
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = null;
        if (_player.LocalSession == null)
            return false;

        return _manager.TryGetInfo(out sponsor);
    }
}
