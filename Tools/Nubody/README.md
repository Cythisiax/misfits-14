# NuBody upstream ownership tooling

This directory defines the immutable upstream boundary for the Shitmed-to-NuBody migration.

The initial source is wizden commit `9c23b4a6d8d3188b8027ae5c0042a31455e31e03`, the stabilized
NuBody snapshot containing the original body and appearance changes, intervening fixes, and the public
API documentation follow-up. It used RobustToolbox `f509405022cf75c3a906b2e1bd0a3e8e7eafe3bc`;
Misfits' newer engine remains pinned independently at `724345afdffcdedebc43577654385a9ecfe3a092`.
This avoids importing unrelated post-NuBody Content refactors into the initial cutover.

`upstream-manifest.json` owns complete Body and Humanoid trees and derives cross-cutting integration
paths from the two original NuBody commits plus the upstream public-API documentation follow-up. The
selection commits must be ancestors of the pin. The manifest explicitly excludes combined
database/model snapshots, map migrations, and downstream `_DV` prototypes that cannot be imported as
independent 1:1 files. Those are integration glue and must be reconciled separately.

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
