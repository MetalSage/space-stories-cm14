using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using UIRange = Robust.Client.UserInterface.Controls.Range;
using Robust.Shared.Maths;

namespace Content.Client._Stories.UserInterface.Control;

[Virtual]
public class STProgressBar : UIRange
{
    private StyleBox? _backgroundStyleBoxOverride;
    private StyleBox? _foregroundStyleBoxOverride;
    private bool _vertical;

    public Label Label { get; }

    public bool Vertical
    {
        get => _vertical;
        set
        {
            if (_vertical != value)
            {
                _vertical = value;
                InvalidateMeasure();
            }
        }
    }

    public StyleBox? BackgroundStyleBoxOverride
    {
        get => _backgroundStyleBoxOverride;
        set
        {
            _backgroundStyleBoxOverride = value;
            InvalidateMeasure();
        }
    }

    public StyleBox? ForegroundStyleBoxOverride
    {
        get => _foregroundStyleBoxOverride;
        set
        {
            _foregroundStyleBoxOverride = value;
            InvalidateMeasure();
        }
    }

    public STProgressBar()
    {
        Label = new Label
        {
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Center,
        };
        AddChild(Label);
    }

    private StyleBox? GetBackground()
    {
        if (BackgroundStyleBoxOverride != null)
            return BackgroundStyleBoxOverride;

        TryGetStyleProperty<StyleBox>("background", out var ret);
        return ret;
    }

    private StyleBox? GetForeground()
    {
        if (ForegroundStyleBoxOverride != null)
            return ForegroundStyleBoxOverride;

        TryGetStyleProperty<StyleBox>("foreground", out var ret);
        return ret;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var bg = GetBackground();
        bg?.Draw(handle, PixelSizeBox, UIScale);

        var fg = GetForeground();
        if (fg != null)
        {
            if (_vertical)
            {
                var size = PixelHeight * GetAsRatio();
                if (size > 0)
                    fg.Draw(handle, UIBox2.FromDimensions(0, PixelHeight - size, PixelWidth, size), UIScale);
            }
            else
            {
                var minSize = fg.MinimumSize;
                var size = PixelWidth * GetAsRatio() - minSize.X;
                if (size > 0)
                    fg.Draw(handle, UIBox2.FromDimensions(0, 0, minSize.X + size, PixelHeight), UIScale);
            }
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var bgSize = GetBackground()?.MinimumSize ?? Vector2.Zero;
        var fgSize = GetForeground()?.MinimumSize ?? Vector2.Zero;
        
        Label.Measure(availableSize);
        var labelSize = Label.DesiredSize;
        
        return Vector2.Max(Vector2.Max(bgSize, fgSize), labelSize);
    }
}
