# NuBody upstream ownership tooling

This directory defines the immutable upstream boundary for the Shitmed-to-NuBody migration.

The initial source is wizden commit `8cf744ec55fa19968ae5a7bc279d5b3862e3f878`, the completed
NuBody body and humanoid-appearance merge point. It used RobustToolbox
`68f8d00931d6b14f3e592d50c47dd44ef09eed1f`;
Misfits' newer engine remains pinned independently at `724345afdffcdedebc43577654385a9ecfe3a092`.
Later upstream NuBody updates are applied only after this compiling initial cutover, so unrelated
post-merge Content refactors cannot silently enter the ownership boundary.

`upstream-manifest.json` owns the complete Body and Humanoid implementation trees plus standalone
assets and migrations introduced by NuBody. Its provenance commits must be ancestors of the pin.
Files in other subsystems that those commits touched are deliberately integration glue: they must be
reconciled against the fork instead of replacing whole historical subsystems. This keeps the NuBody
implementation byte-identical while preventing unrelated Wizden Content history from becoming part
of the ownership boundary.

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
