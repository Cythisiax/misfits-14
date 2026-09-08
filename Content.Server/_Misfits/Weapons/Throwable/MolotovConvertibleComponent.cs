namespace Content.Server._Misfits.Weapons.Throwable;

/// <summary>
/// Marks a bottle as able to receive a cloth wick and records which solution becomes its fuel.
/// </summary>
[RegisterComponent]
public sealed partial class MolotovConvertibleComponent : Component
{
    /// <summary>
    /// The bottle's solution-container ID.
    /// </summary>
    [DataField]
    public string Solution = "drink";
}
