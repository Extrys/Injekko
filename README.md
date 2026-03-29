# Injekko

Injekko is now organized as a Unity package repository.

The repo keeps only the pieces that are still source-of-truth for the Unity-first workflow:

- [UnityPackage/com.extrys.injekko](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko): the actual Unity package content
- [InjekkoGen](/C:/Users/Extrys/source/repos/Injekko/InjekkoGen): the Roslyn source generator that produces `_Rizolver`, `_Fucktory` and graph metadata
- [tools/StageUnityAnalyzer.ps1](/C:/Users/Extrys/source/repos/Injekko/tools/StageUnityAnalyzer.ps1): rebuilds the generator and replaces the analyzer DLLs inside the package

## Repo Layout

The Unity package lives here:

- [package.json](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko/package.json)

That folder is what Unity should consume as a local package.

The generator project stays outside the package on purpose:

- Unity needs the compiled analyzer DLLs
- the repo still needs generator source code as the real editable implementation
- the package should receive built analyzer artifacts, not the generator project itself

## Daily Workflow

1. Edit package runtime/editor code under [UnityPackage/com.extrys.injekko](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko).
2. Edit source-generator code under [InjekkoGen](/C:/Users/Extrys/source/repos/Injekko/InjekkoGen) when generator behavior changes.
3. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\StageUnityAnalyzer.ps1
```

That command rebuilds `InjekkoGen` and automatically replaces the analyzer DLLs inside:

- [UnityPackage/com.extrys.injekko/Analyzers](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko/Analyzers)

So yes: if you work this way, the package DLL changes are expected repo changes. That is normal, because the Unity package consumes those compiled analyzer artifacts directly.

## What Was Removed

The old local playground, mock Unity runtime, duplicated runtime projects and editor-side duplicate project were removed from the repo because they were no longer the source of truth after the move to a real Unity package workflow.

## Current Direction

- Unity-only
- zero reflection in runtime
- `[Injek]` as the main public pseudo-constructor API
- generated `_Rizolver` and `_Fucktory`
- Unity scope tree and future Graph Toolkit tooling
