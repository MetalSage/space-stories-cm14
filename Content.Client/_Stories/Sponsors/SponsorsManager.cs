using System.Diagnostics.CodeAnalysis;
using Content.Shared._Stories.Sponsors;

namespace Content.Client._Stories.Sponsors;

public sealed class SponsorsManager
{
    private SponsorInfo? _info;

    public void Initialize()
    {
    }

    public void SetSponsorInfo(SponsorInfo? info)
    {
        _info = info;
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = _info;
        return _info != null;
    }
}
