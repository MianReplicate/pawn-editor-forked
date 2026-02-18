# Changelog

## v2.1 - 2026-02-17

All fixes below are applied to the community fork.

### Social compatibility identity fix

- Cloned pawns now keep a stable social compatibility seed from their original saved ThingID.
- New clones still receive a unique new ThingID (no duplicate entity IDs).
- `Pawn_RelationsTracker.CompatibilityWith()` is patched to use the saved compatibility seed offset instead of the remapped clone ThingID offset.
- Method detail: replace pair offset source from `ConstantPerPawnsPairCompatibilityOffset(otherPawn.thingIDNumber)` to `ConstantPerPawnsPairCompatibilityOffset(CompatibilitySeedFor(otherPawn))` when seed exists.

### Critical fixes

- **#001 IntField reset** (`UIUtility.cs`): `intBuff` initializes from current value and control naming is stable.
- **#002 Favorite color Def mutation** (`LeftList.cs`): color picker assigns nearest `ColorDef` reference instead of mutating shared `ColorDef.color`.
- **#004 Missing body part kills pawn** (`ListingMenu_Hediffs.cs`): no default to core part for `Hediff_MissingPart`; requires valid non-core part.

### High-severity fixes

- **#016 Dev toolbar button stability** (`PawnEditorMod.cs`): uses safe postfix path (no fragile IL dependency).
- **#019 Duplicate pawn IDs** (`SaveLoadPatches.cs`): `ReassignLoadID` handles `loadID` and `id/thingIDNumber` safely with Thing-parent guard.

### Medium-severity fixes

- **#007 Child backstory missing** (`TopRightButtons.cs`): handles child/adult transition backstories safely.
- **#008 Copy/Paste item loss** (`CopyPaste.cs`): preserves stack count, quality, and color; uses safe iteration snapshots.
- **#009 Scenario items lost** (`StartingThingsManager.cs`): snapshots scenario parts and restores safely on failure.
- **#010 Xenotype NullReferenceException** (`TopRightButtons.cs`): null checks and safe gene list handling.
- **#012 Invisible weapons after load** (`TabWorker_Gear.cs`): on-load callbacks recache graphics and clear gear caches.
- **#018 Mechs go rogue** (`LeftPanel.cs`): auto-assigns available Mechanitor as Overseer during pregame add.

### UI / behavior policy

- Top-right Pawn Editor button: requires **Dev Mode**.
- Selected pawn `Edit` gizmo: requires **Dev Mode + God Mode**.
- Social tab defaults to show all relations on first open.

### Known issues not yet addressed

- #005 Relationship tab can still miss entries in some pregame contexts.
- #011 Tabs may not refresh when switching pawns in specific flows.
- #013 Gene list can be incomplete in edge cases.
- #014 Pawn names and spaces are still limited by vanilla IMGUI behavior.
- #017 Social stats reset on game launch requires more runtime investigation.
