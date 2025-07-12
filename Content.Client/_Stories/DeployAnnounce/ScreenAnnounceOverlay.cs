namespace Content.Client._RMC14.Marines.ScreenAnnounce;

public sealed class ScreenAnnounceOverlay : Overlay
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private ScreenAnnounceControl _control;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ScreenAnnounceOverlay()
    {
        IoCManager.InjectDependencies(this);
        _control = new ScreenAnnounceControl();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        if (_control.Parent == null)
        {
            _uiManager.RootControl.AddChild(_control);
        }
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();
        _control.Orphan();
    }

    public void UpdateAnnouncement(string[] announceText)
    {
        _control.UpdateAnnouncement(announceText);
    }
}
