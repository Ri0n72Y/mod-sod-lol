# Phase 0 Test Results — 2026-08-12

> Tested head: `ea4fa8d3616809231e885d863e20d4f940f3e0dc`  
> Machine: local Windows PC  
> Game: Shape of Dreams `6000.0.58f2 (92dee566b325)` (Unity 6)  
> Game directory: `D:\Games\Steam\steamapps\common\Shape of Dreams`

## Execution summary

| Item | Result |
|---|---|
| Branch checkout | PASS — `experiment/caitlyn-identity-poc @ ea4fa8d` |
| P0-01 local Release compilation | **PASS** |
| Static runtime API validation | **FAIL on tested head** — two deterministic load blockers found |
| P0-02 through P0-25 | Not run because the tested head would fail during mod loading |

## P0-01 — PASS

The local Visual Studio 2022 Community install did not include a complete Roslyn / .NET Framework 4.8 targeting setup, so the assembly was built with the NuGet compiler toolchain instead:

- `Microsoft.Net.Compilers.Toolset 5.6.0`
- Roslyn `csc.exe` from `tasks/net472`
- `-nostdlib+`
- framework and game references taken from `Shape of Dreams_Data\Managed`
- output: `bin\Release\SodLolCaitlyn.dll`
- result: 0 errors, 0 warnings

Working git-bash command:

```bash
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'
GAME="/d/Games/Steam/steamapps/common/Shape of Dreams/Shape of Dreams_Data/Managed"
./toolset/tasks/net472/csc.exe -nologo -target:library -nostdlib+ \
  -out:bin/Release/SodLolCaitlyn.dll -langversion:12 -define:TRACE -optimize+ -warn:4 \
  -r:"$GAME/mscorlib.dll" -r:"$GAME/netstandard.dll" \
  -r:"$GAME/System.dll" -r:"$GAME/System.Core.dll" -r:"$GAME/System.Xml.dll" \
  -r:"$GAME/System.Configuration.dll" -r:"$GAME/System.Numerics.dll" \
  -r:"$GAME/System.Runtime.Serialization.dll" \
  -r:"$GAME/0Harmony.dll" -r:"$GAME/Assembly-CSharp.dll" \
  -r:"$GAME/Dew.Contents.dll" -r:"$GAME/Dew.Core.dll" -r:"$GAME/Dew.UI.dll" \
  -r:"$GAME/Mirror.dll" -r:"$GAME/Sirenix.Serialization.dll" \
  -r:"$GAME/UnityEngine.dll" -r:"$GAME/UnityEngine.CoreModule.dll" \
  src/*.cs src/patches/*.cs
```

If the .NET Framework 4.8.1 developer components are installed, the project should also support the normal MSBuild route documented in `docs/test-plan.md`.

## Static API validation against installed DLLs

Validation was performed with dnlib 4.5.0 against the actual installed game assemblies. This matters because Harmony target resolution and private Mirror reflection are runtime concerns and are not checked by the C# compiler.

### Confirmed on the tested game build

- `DewResources.Load(System.String, ResourceLoadSettings)` exists.
- `LootManager.OnStartServer` exists and is unambiguous by name.
- `NetworkIdentity._assetId` exists as a non-public instance field.
- `NetworkIdentity.InitializeNetworkBehaviours` exists as a non-public instance method.
- Vanilla proxy templates exist: `St_D_DoubleTap`, `St_Q_HandCannon`, `St_R_QuickTrigger`, `St_R_PrecisionShot`.
- `Hero_Lacerta`, `ResourceLoadSettings`, and `DewInternal.DewResourceDatabase` exist.
- `DewSave.onSaveStarted` and `DewSave.onSaveEnded` exist.

### Blocker 1 on `ea4fa8d`: wrong `Actor.DoBasicAttackHit` Harmony target

The tested head patched a five-argument overload. The installed `Dew.Core.dll` exposes the current method as:

```text
DoBasicAttackHit(
    Actor,
    Entity from,
    Entity to,
    bool isCriticalHit,
    bool isMain,
    float damage,
    float attackEffect,
    ActionRef<DamageData> onBeforeDispatch)
```

Consequence on the tested head: `harmony.PatchAll()` cannot resolve the target and mod startup aborts before P0-02.

**Resolution after this report:** `CaitlynPatches.cs` now targets the tested eight-argument overload and forwards `to` plus `isMain` to the Headshot controller. Recompile/retest required.

### Blocker 2 on `ea4fa8d`: obsolete `NetworkIdentity._isSceneObject` reflection

The installed Mirror `NetworkIdentity` has `_assetId` but no `_isSceneObject` field. The tested head therefore throws `MissingFieldException` in resource registration after the Harmony issue is fixed.

**Resolution after this report:** `_isSceneObject` reflection was removed. Runtime prefab setup now sets `_assetId` and `sceneId = 0`; `_assetId` and `InitializeNetworkBehaviours` remain fail-closed reflection dependencies. Recompile/retest required.

## Additional deployment alignment found during fix

The tested working compiler command writes `bin\Release\SodLolCaitlyn.dll`, while the previous metadata path referenced `obj/Release/*.dll`. The branch metadata has been changed to `bin/Release/*.dll` so the Mod Manager test path matches both the project output and the verified manual compiler output.

## Next run

The previous P0-01 PASS applies specifically to `ea4fa8d`. Because runtime code changed after that test, the current PR head must be compiled again before P0-02.

After recompilation:

1. confirm the current DLL is under `bin\Release`;
2. place/clone the project under `<GameFolder>\Mods\` so `about\metadata.json` and the configured assembly glob resolve;
3. execute P0-02 through P0-20 in single player;
4. execute P0-21/P0-22 for host/client behavior;
5. execute P0-23 through P0-25 only on a disposable development copy.
