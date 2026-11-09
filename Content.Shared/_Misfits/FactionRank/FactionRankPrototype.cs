// #Cythisiax Added - Faction rank: one rank in a faction (e.g. NCR First Sergeant, 1SG, E-8).
// Prefix goes in front of the name in chat/radio, name and paygrade show on examine. If the rank
// has an insignia, that pin is what carries the rank: whoever wears it gets the prefix. Ranks
// without one ride on the ID tag instead. The ID's job title is never touched.
// Color is the faction color the prefix shows in, in local chat, whispers and radio.

using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.FactionRank;

[Prototype("factionRank")]
public sealed partial class FactionRankPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public string Prefix = string.Empty;

    [DataField]
    public string? Paygrade;

    [DataField]
    public EntProtoId? Insignia;

    [DataField]
    public Color? Color;
}
