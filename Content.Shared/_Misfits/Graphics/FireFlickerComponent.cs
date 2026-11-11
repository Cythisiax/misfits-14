// #Cythisiax Added - Fire flicker: put this next to a PointLight to make it breathe like a fire instead
// of strobing between random values. Ported from Gruntz's LightFlicker: two sine waves on a
// golden-ratio beat for the wander, a chance every half second of a deeper dip that also pulls the
// radius in, and optional held "steps" for lights that sag and recover. Everything is seeded from the
// entity's network id and server time, so every client sees the same flicker with nothing networked.
// Anything burning through FireVisuals (people, crates, N14 bonfires) gets the defaults automatically;
// put the component on the burning entity to tune its fire light. Defaults match the Gruntz torch.

namespace Content.Shared._Misfits.Graphics;

[RegisterComponent]
public sealed partial class FireFlickerComponent : Component
{
    [DataField]
    public float Amount = 0.2f;

    [DataField]
    public float Speed = 7f;

    [DataField]
    public float DipChance = 0.1f;

    [DataField]
    public float DipDepth = 0.5f;

    [DataField]
    public float StepAmount;

    [DataField]
    public float StepHoldMinSeconds = 0.3f;

    [DataField]
    public float StepHoldMaxSeconds = 1.2f;

    [DataField]
    public int Steps = 3;

    [ViewVariables]
    public uint Seed;

    [ViewVariables]
    public bool Seeded;

    [ViewVariables]
    public float BaseEnergy;

    [ViewVariables]
    public float BaseRadius;

    [ViewVariables]
    public float LastEnergy = float.NaN;

    [ViewVariables]
    public float LastRadius = float.NaN;
}
