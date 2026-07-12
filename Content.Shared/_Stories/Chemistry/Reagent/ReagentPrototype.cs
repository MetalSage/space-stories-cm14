// ReSharper disable CheckNamespace

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    [DataField]
    public bool Explosive;

    [DataField]
    public float Power;

    [DataField]
    public float FalloffModifier;

    [DataField]
    public Color? BurnColor;

    [DataField]
    public float Burncolormod;

    [DataField]
    public bool FirePenetrating;
}
