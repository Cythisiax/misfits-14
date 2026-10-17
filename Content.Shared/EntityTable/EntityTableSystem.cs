using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.EntityTable;

public abstract partial class EntityTableSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
// why is this even in shared??
    public IEnumerable<EntProtoId> GetSpawns(EntityTableSelector? table)
    {
        if (table == null)
            return new List<EntProtoId>();
        SetSeed(_random);
        return table.GetSpawns(_random, EntityManager, _prototypeManager);
    }

    public virtual void SetSeed(IRobustRandom rand)
    {
        rand.SetSeed(_random.Next(int.MinValue, int.MaxValue));
    }
}
