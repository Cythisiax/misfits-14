using Content.Shared.Clothing.Loadouts.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Prototypes;

// #Cythisiax Added - Keep Misfits species restrictions outside the upstream-owned NuBody path.
public sealed partial class SpeciesPrototype
{
    /// <summary>
    ///     If true, only whitelisted players can select this species.
    /// </summary>
    [DataField]
    public bool WhitelistRequired;

    /// <summary>
    ///     If set, this species can only select jobs in this list.
    ///     All other jobs will be unavailable.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>>? RestrictedJobs;

    /// <summary>
    ///     If set, players job-whitelisted for this job will have this species unlocked,
    ///     even without a general server whitelist.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? JobWhitelistUnlock;

    /// <summary>
    ///     Sort order in the species dropdown. Lower values appear first.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    ///     If true, the Antags, Traits, and Markings tabs are hidden
    ///     in the character editor for this species.
    /// </summary>
    [DataField]
    public bool RestrictedCustomization;

    /// <summary>
    ///     If set, only loadouts in these categories will be shown.
    ///     Null means all categories are allowed.
    /// </summary>
    [DataField]
    public List<ProtoId<LoadoutCategoryPrototype>>? AllowedLoadoutCategories;

    /// <summary>
    ///     If set, only traits in these categories will be shown.
    ///     Null means all categories are allowed.
    /// </summary>
    [DataField]
    public List<ProtoId<TraitCategoryPrototype>>? AllowedTraitCategories;

    /// <summary>
    ///     If set, only these exact trait IDs will be shown, regardless of category.
    ///     Takes precedence over <see cref="AllowedTraitCategories"/> so a restricted species
    ///     can allow a single specific perk (e.g. Mr Handy + Italian Accent).
    /// </summary>
    [DataField]
    public List<ProtoId<TraitPrototype>>? AllowedTraits;
}
