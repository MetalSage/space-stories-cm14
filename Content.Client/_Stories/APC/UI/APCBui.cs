using JetBrains.Annotations;
using Content.Shared._Stories.APC.UI;

namespace Content.Client._Stories.APC.UI;


[UsedImplicitly]
public sealed class APCControlBui : BoundUserInterface
{
    private APCControlWindow _window;

    public APCControlBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new APCControlWindow(owner);
    }

    protected override void Open()
    {
        base.Open();
        _window.OnClose += Close;

        _window.OpenCentered();

    }

    protected override void Dispose(bool disposing)
    {
        _window?.Close();
        base.Dispose(disposing);
    }
}
