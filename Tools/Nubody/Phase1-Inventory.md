# NuBody Phase 1 inventory

Snapshot date: 2026-08-19

- Misfits baseline: `377b7b81322a7c670954917d4d004fcea7487723`
- Pinned wizden NuBody source: `65479ed69cd522834dbcb69da3302cd654036423`
- Source RobustToolbox: `724345afdffcdedebc43577654385a9ecfe3a092`
- Misfits RobustToolbox: `724345afdffcdedebc43577654385a9ecfe3a092`
- Upstream-owned parity scope: 437 paths
- Shitmed-owned tracked files: 460
- External textual Shitmed references: 648 lines across 136 files
  - `Content.Client`: 16 files
  - `Content.Server`: 27 files
  - `Content.Shared`: 27 files
  - `Resources`: 66 files

Regenerate these values with `Get-ShitmedInventory.ps1` and `Test-NubodyParity.ps1`; do not treat the
snapshot counts as the source of truth after the branch changes.

## Downstream code embedded in future upstream-owned paths

The following behavior must not survive as edits to the imported wizden files:

- Roundstart prosthetics currently connect through `SharedBodySystem.Body.cs` and
  `SharedHumanoidAppearanceSystem.cs`. Rebuild the subscription from `_Misfits` against NuBody's
  profile/organ events during the cutover.
- Misfits suffocation configuration currently edits `RespiratorSystem.cs`. Move policy into a
  downstream subscriber or adapter once the pinned NuBody respirator API is present.
- Genetics currently gains private access through modified `Access` attributes on bloodstream,
  metabolizer, and thermal-regulator components. Retarget the downstream systems to public NuBody
  APIs; do not retain those attributes in upstream-owned components.
- Bloodstream chat behavior currently edits `BloodstreamSystem.cs`. Move the chat event emission to
  a downstream event subscriber or a separately owned adapter.
- Humanoid appearance contains downstream bark/TTS, size, custom-species, profile-cloning, and
  skin-color behavior. Extract compatible data fields into downstream partial declarations and adapt
  behavior through public APIs before importing the corresponding upstream files.
- `HumanoidSkinColor.TintedHuesSkin` cannot be added through a partial enum. Downstream species using
  it need a separate coloration component/system or must map to an upstream-supported coloration;
  keeping the enum edit would violate 1:1 parity.
- Body prototypes contain downstream changes such as `N14Virus`, moth fruit digestion, and skeleton
  sprite scaling. Re-express them under `_Misfits` / `_Nuclear14` through downstream prototypes or
  organ replacements after the NuBody prototype IDs are available.

## Safe extraction completed

Misfits species whitelist, job restriction, customization, loadout, and trait fields were moved from
`Content.Shared/Humanoid/Prototypes/SpeciesPrototype.cs` to the downstream partial declaration at
`Content.Shared/_Misfits/Humanoid/Prototypes/SpeciesPrototype.Misfits.cs`. This preserves the current
serialized field contract while removing one block of fork state from a future upstream-owned path.
