# NuBody upstream ownership tooling

This directory defines the immutable upstream boundary for the Shitmed-to-NuBody migration.

The initial source is wizden commit `65479ed69cd522834dbcb69da3302cd654036423`, a reviewed
post-NuBody snapshot on RobustToolbox `724345afdffcdedebc43577654385a9ecfe3a092`. Misfits uses the
same engine commit. Pinning a coherent source snapshot, rather than the historical NuBody merge
commit, is required for a replace-and-verify upstream update workflow.

`upstream-manifest.json` owns the complete Body and Humanoid implementation trees plus standalone
assets and migrations introduced by NuBody. Its provenance commits must be ancestors of the pin.
Files in other subsystems that those commits touched are deliberately integration glue: they must be
reconciled against the fork instead of replacing whole historical subsystems. This keeps the NuBody
implementation byte-identical while preventing unrelated Wizden Content history from becoming part
of the ownership boundary.

This is explicitly a NuBody-only migration, not a full Wizden Content rebase. Missing APIs required
by the byte-identical implementation belong in the manifest's `_Misfits/NubodyCompat` roots as narrow
adapters over the fork's existing systems. The adapters may change when the NuBody pin advances, but
the upstream-owned files may not. Do not solve adapter failures by claiming entire transitive Wizden
subsystems as NuBody-owned.

Run the parity check from the repository root:

```powershell
pwsh -File Tools/Nubody/Test-NubodyParity.ps1
```

The command exits nonzero if any upstream-owned file is missing, different, or unexpectedly retained.
Before the atomic cutover, differences are expected and can be inspected without failing the command:

```powershell
pwsh -File Tools/Nubody/Test-NubodyParity.ps1 -AllowDifferences -SummaryOnly
```

Regenerate the live Shitmed reference inventory as JSON with:

```powershell
pwsh -File Tools/Nubody/Get-ShitmedInventory.ps1
```

Use `-SummaryOnly` when only counts are needed. The inventory excludes serialized maps and the seven
Shitmed-owned roots themselves when finding external textual references.

Preview the guarded atomic synchronization with:

```powershell
pwsh -File Tools/Nubody/Sync-Nubody.ps1
```

The sync refuses to run outside `feature/nubody-migration`, without the archive tag, or with a dirty
working tree. After reviewing its counts, `-Apply` imports the pinned files and stages removal of
upstream-replaced legacy paths and the seven Shitmed roots.
