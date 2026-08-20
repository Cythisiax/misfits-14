using Robust.Shared.Serialization;

namespace Content.Shared.DisplacementMap;

// #Cythisiax Added - NuBody compatibility contract kept outside the byte-identical upstream-owned trees.
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DisplacementData
{
    [DataField(required: true)]
    public Dictionary<int, PrototypeLayerData> SizeMaps = new();

    [DataField]
    public string? ShaderOverride = "DisplacedDraw";

    [DataField]
    public string ShaderOverrideUnshaded = "DisplacedDrawUnshaded";
}
