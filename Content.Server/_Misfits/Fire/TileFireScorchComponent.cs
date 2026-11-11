// #Cythisiax Added - Fire scorch: lives on a grid and remembers which tiles have already burnt down, so
// a grass fire moves on as a front instead of sloshing back and forth over the same ground. Tiles
// recover after the burning fire's scorchSeconds. Fuel puddles ignore this; only ground and loose
// flammable junk count as "burnt".

namespace Content.Server._Misfits.Fire;

[RegisterComponent]
public sealed partial class TileFireScorchComponent : Component
{
    [ViewVariables]
    public Dictionary<Vector2i, TimeSpan> BurntUntil = new();
}
