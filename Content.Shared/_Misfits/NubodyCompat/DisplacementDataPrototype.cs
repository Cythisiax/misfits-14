using Robust.Shared.Prototypes;

namespace Content.Shared.DisplacementMap;

// #Cythisiax Added - NuBody compatibility prototype kept outside the byte-identical upstream-owned trees.
[Prototype]
public sealed partial class DisplacementDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DisplacementData Displacement = default!;
}
