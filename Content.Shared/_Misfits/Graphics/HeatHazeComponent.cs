// #Cythisiax Added - Heat haze: put this on anything that should make the air shimmer above it
// (bonfires, burning barrels, fireplaces). Radii are in tiles. If the entity also reports
// FireVisuals.OnFire through its appearance (N14 bonfires), the haze only shows while it is lit.
// Anything that is on fire gets haze automatically without this component; this is for fires
// that are always burning or need a bigger shimmer than the default.


namespace Content.Shared._Misfits.Graphics;

[RegisterComponent]
public sealed partial class HeatHazeComponent : Component
{
    [DataField]
    public float InnerRadius = 0.35f;

    [DataField]
    public float OuterRadius = 2.2f;

    [DataField]
    public float Strength = 1f;
}
