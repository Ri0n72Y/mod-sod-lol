# Phase 0 Test Plan

This branch compiles against the locally installed Shape of Dreams managed assemblies and its important behavior occurs inside the running game. Repository-only CI cannot replace the local build and in-game validation described below.

First local validation on 2026-08-12 is recorded in `docs/test-results-2026-08-12.md`. The tested head `ea4fa8d` passed P0-01 but exposed two deterministic runtime API mismatches before P0-02; both have since been patched. The current head therefore requires a fresh P0-01 build before continuing.

## Build prerequisites

- Shape of Dreams installed locally.
- Developer Mode enabled in game.
- Preferred: MSBuild / Visual Studio with .NET Framework 4.8.1 support.
- Alternative already proven locally: `Microsoft.Net.Compilers.Toolset 5.6.0` Roslyn `csc.exe` with `-nostdlib+` and references from the game's `Managed` directory.
- `ShapeOfDreamsHome` points to the Shape of Dreams installation directory when using MSBuild.

MSBuild example:

```powershell
$env:ShapeOfDreamsHome = "D:\Games\Steam\steamapps\common\Shape of Dreams"
msbuild .\SodLolCaitlyn.csproj /p:Configuration=Release
```

The validated fallback `csc` command is preserved in `docs/test-results-2026-08-12.md`.

The mod metadata loads `bin/Release/*.dll`. Clone/copy the repository into a subfolder under `<ShapeOfDreamsHome>\Mods` so `about/metadata.json` and `bin/Release/SodLolCaitlyn.dll` resolve from the same mod root.

## Current status

| Area | Status |
|---|---|
| Static code review | PASS for current design; runtime signatures updated from installed DLL inspection |
| P0-01 on `ea4fa8d` | PASS — 0 errors / 0 warnings |
| P0-01 on current head | NOT RUN — required after API fixes |
| P0-02 through P0-25 | NOT RUN on current head |

## Validation matrix

| ID | Test | Expected |
|---|---|---|
| P0-01 | Build Release | No compile errors against the currently installed game assemblies. |
| P0-02 | Load mod from title screen | Mod loads, game marks the lobby as gameplay-altering, no exception from Harmony/resource registration. |
| P0-03 | Inspect startup logs/resource registry | All five runtime types register once; all vanilla proxy templates/configs resolve; mutable config isolation verification passes. |
| P0-04 | Select Lacerta and inspect Identity choices | Caitlyn Headshot is reachable from Lacerta's trait loadout. Text/icon may still be placeholder/missing. |
| P0-05 | Select another Traveler | Caitlyn Headshot is absent from that Traveler's `loadoutTrait`. |
| P0-06 | Start a run without Headshot | Caitlyn Q/W/E/R type names are absent from the authoritative server loot pool. |
| P0-07 | Start a run with Headshot | Caitlyn Q/W/E/R type names are added to the authoritative shared server loot pool. |
| P0-08 | Obtain one custom active Memory | Resource resolves to the correct custom `SkillTrigger` subclass and can be picked/equipped without a missing-resource error. Other players are allowed to use generated Caitlyn Memories. |
| P0-09 | Cast Q/E/R proxy | The isolated proxy `TriggerConfig` path executes without a network/prefab exception. Final Caitlyn behavior is not expected yet. |
| P0-10 | Compare vanilla proxy source skill before/after custom cast | Vanilla cooldown/charge/cast configuration remains unchanged; Caitlyn proxy runtime state does not leak back into the vanilla skill. |
| P0-11 | Disable/unload mod | Custom loot entries disappear, only Headshot is removed from Lacerta's trait list, runtime mappings/handlers are removed, unrelated loadout changes survive. |
| P0-12 | Re-enable mod in the same process | No duplicate GUID/type/asset-id registration exception; Headshot returns to Lacerta exactly once. |
| P0-13 | Trigger a normal game save while the mod is enabled | During serialization, mod-owned profile/stat/dejavu keys are absent; after `onSaveEnded`, runtime-only entries return. |
| P0-14 | Exit after saving with the mod enabled, then inspect/restart without the mod | Saved profile data contains no Caitlyn-only unlock/stat/dejavu keys and vanilla startup does not reference missing custom skill types. |
| P0-15 | With Headshot equipped, land Lacerta basic attacks | The patched eight-argument `Actor.DoBasicAttackHit` path forwards target `to`; only `isMain == true` calls advance the synced counter; every fifth qualifying call produces one server bonus-damage event and then resets the counter. |
| P0-16 | Use Q/R/other non-basic abilities with Headshot equipped | Non-basic abilities do not advance Headshot merely because they deal damage or trigger other attack-related events. |
| P0-17 | Produce secondary/non-main basic attack hits if available | `isMain == false` calls do not add Headshot stacks. |
| P0-18 | Remove/change away from Headshot | Server removes `sodlol.caitlyn.headshot.count`; later basic attacks do not proc the passive. |
| P0-19 | Compare displayed crit chance with Headshot debug log | `critRaw` matches the game's normalized runtime stat convention; `critRatio` equals the same value clamped to `0..1`. Also observe `isCriticalHit` independently so crit occurrence is not confused with crit chance stat scaling. |
| P0-20 | Make the fifth qualifying basic attack itself kill the target | Headshot is consumed, the postfix detects `target.isDestroyed`, no second `DealDamage` call is sent to the destroyed Actor, and no lifecycle/null exception is logged. |
| P0-21 | Host + one client with Headshot | Bonus damage is applied once by the server; client does not independently duplicate damage; synced counter is observable on both sides. |
| P0-22 | Host + one client and obtain/cast a runtime Memory | Mirror runtime prefab spawns correctly. Runtime registration relies on `_assetId`, `sceneId = 0`, and `InitializeNetworkBehaviours`; missing required private API must fail loudly instead of silently creating a broken network object. |
| P0-23 | Simulate a Dew resource/type/asset-id collision in a disposable development copy | Mod refuses to overwrite the existing mapping and startup rollback leaves no partial Caitlyn mappings, prefabs, handlers or Harmony patches. |
| P0-24 | Break one required vanilla proxy template name in a disposable development copy | Registration aborts and rolls back; the mod does not expose a custom Memory with missing `TriggerConfig`. |
| P0-25 | Cause a later initialization failure after resource registration in a disposable development copy | `CaitlynContent` rollback removes profile hooks/runtime keys, loadout injection, loot entries, Mirror handlers and Dew resource mappings. |

## Runtime API gates confirmed on game build `6000.0.58f2 (92dee566b325)`

Installed-DLL inspection confirmed:

- `DewResources.Load(string, ResourceLoadSettings)`;
- `LootManager.OnStartServer`;
- current eight-argument `Actor.DoBasicAttackHit(Actor, Entity, Entity, bool, bool, float, float, ActionRef<DamageData>)`;
- `NetworkIdentity._assetId`;
- `NetworkIdentity.InitializeNetworkBehaviours`;
- no `NetworkIdentity._isSceneObject` field in this Mirror build;
- all four vanilla proxy template types;
- `DewSave.onSaveStarted` / `DewSave.onSaveEnded`.

These checks supplement compilation because Harmony target resolution and reflection dependencies are runtime-bound.

## Experimental Headshot tuning

The Phase-0 passive is a Shape of Dreams adaptation used to prove the Hero basic-attack/state/damage path. It currently triggers every fifth qualifying main basic-attack hit and deals bonus damage equal to:

```text
AttackDamage × (0.60 + 1.50 × Clamp01(CritChance))
```

Shape of Dreams stores crit chance as a normalized decimal in the runtime stat model (for example `0.005` is 0.5%). Values outside the probability domain are clamped for this experiment.

Its bonus damage uses `procCoefficient = 0` to avoid introducing unrelated proc-chain behavior. Because the Headshot hook is a postfix, a fifth attack that already destroys its target consumes the proc but skips the second damage request. W/E trap/net Headshot interactions are deferred until those Memories have real implementations.

## Static invariants

Before an in-game run is considered meaningful, the code enforces these invariants at startup:

- no custom GUID duplicates inside the mod;
- no custom runtime type duplicates inside the mod;
- no generated Mirror asset-id duplicates inside the mod;
- no overwrite of an existing Dew type/GUID or network asset-id mapping;
- every required vanilla proxy template exists and exposes non-null `TriggerConfig` data;
- Caitlyn and vanilla top-level `TriggerConfig` objects are separate references;
- known mutable `castMethod`, `channel`, `predictionSettings`, `selfValidator` and `targetValidator` objects are not shared when they are reference types;
- missing Mirror `_assetId` or `InitializeNetworkBehaviours` aborts registration instead of being ignored;
- profile persistence hooks must exist before runtime-only profile keys are registered;
- loot-pool mutation occurs only on the server;
- partial registration or later content initialization failure enters rollback.

## Useful observations to capture

When a test fails, record:

- exact PR head SHA;
- game version;
- host/client role;
- exact exception and full stack trace from the developer console/log;
- selected Traveler and Identity;
- whether failure happens in lobby, load, save, pickup, equip, cast, attack, unload, reconnect or rollback;
- the custom runtime type/GUID/asset-id mentioned in the failure;
- for Headshot tests, `critRaw`, `critRatio`, counter value, `isCriticalHit`, and whether the hit was main/secondary.

## Phase 0 acceptance threshold

The infrastructure is viable for Phase 1 combat work when P0-01 through P0-20 pass in single-player. Multiplayer tests P0-21/P0-22 may remain a follow-up only if the failure is isolated to Mirror runtime-prefab compatibility and does not invalidate resource, loadout, profile or single-player gameplay registration.

P0-23 through P0-25 are destructive development tests and should be run only on a disposable development copy/configuration. Their expected result is deterministic rollback.

After that threshold, implement the remaining real kit one ability at a time in this order:

1. Q Piltover Peacemaker.
2. E 90 Caliber Net.
3. W Yordle Snap Trap.
4. R Ace in the Hole.

Q/E are prioritized because they exercise projectile and displacement primitives; W/R then add persistent world state and channel/target-lock behavior.
