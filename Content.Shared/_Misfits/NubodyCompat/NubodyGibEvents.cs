namespace Content.Shared.Gibbing;

/// <summary>
/// Allows NuBody organs to contribute their giblets to the fork's gibbing operation.
/// </summary>
// #Cythisiax Added - Compatibility event consumed by byte-identical NuBody systems.
[ByRefEvent]
public readonly record struct BeingGibbedEvent(HashSet<EntityUid> Giblets);

/// <summary>
/// Raised immediately before a gibbed entity is queued for deletion.
/// </summary>
// #Cythisiax Added - Compatibility event consumed by byte-identical NuBody systems.
[ByRefEvent]
public readonly record struct GibbedBeforeDeletionEvent(HashSet<EntityUid> Giblets);
