using Content.Server.NPC.HTN;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Misfits.EntityTable;

public sealed partial class EntityTableSystem : Content.Shared.EntityTable.EntityTableSystem
{
    [Dependency] private HTNSystem _htn = default!;

    public override void SetSeed(IRobustRandom rand)
    {
        rand.SetSeed(rand.Next());
    }

}
