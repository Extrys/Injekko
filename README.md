# STILL WORK IN PROGRESS!!!

Injekko is being developed alongside a game I’m currently working on, both to validate it in production and to demonstrate that it can be used in real projects.
The project is still in development, and I’ll continue improving it over time as I use it across my own projects.

I still need to add documentation on how to use this new approach. Now it uses graphs for codegen dependency management. It uses Unity Graph Toolkit.


# Injekko

Injekko is now organized as a Unity package repository.

The repo keeps only the pieces that are still source-of-truth for the Unity-first workflow:

- [UnityPackage/com.extrys.injekko](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko): the actual Unity package content
- [InjekkoGen](/C:/Users/Extrys/source/repos/Injekko/InjekkoGen): the Roslyn source generator that produces `_Rizolver`, `_Fucktory` and graph metadata
- [tools/StageUnityAnalyzer.ps1](/C:/Users/Extrys/source/repos/Injekko/tools/StageUnityAnalyzer.ps1): optional manual fallback that rebuilds the generator and replaces the analyzer DLLs inside the package

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
3. Build [InjekkoGen.csproj](/C:/Users/Extrys/source/repos/Injekko/InjekkoGen/InjekkoGen.csproj).

The generator project now has a post-build target that automatically stages these DLLs into the Unity package:

- `InjekkoGen.dll`
- `Microsoft.CodeAnalysis.dll`
- `Microsoft.CodeAnalysis.CSharp.dll`

That means the normal local flow is now just “edit generator -> build generator -> Unity picks up the updated analyzer”.

If you want to stage manually for any reason, you can still run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\StageUnityAnalyzer.ps1
```

That command rebuilds `InjekkoGen` and also replaces the analyzer DLLs inside:

- [UnityPackage/com.extrys.injekko/Analyzers](/C:/Users/Extrys/source/repos/Injekko/UnityPackage/com.extrys.injekko/Analyzers)

So yes: if you work this way, the package DLL changes are expected repo changes. That is normal, because the Unity package consumes those compiled analyzer artifacts directly.

## CI Strategy

The recommended policy for this repo is:

- local build stages analyzer DLLs into the package automatically
- CI rebuilds the generator in a clean environment
- CI fails if the tracked analyzer DLLs in the package are not the ones produced from source

That validation workflow lives in:

- [.github/workflows/validate-analyzer-sync.yml](/C:/Users/Extrys/source/repos/Injekko/.github/workflows/validate-analyzer-sync.yml)

This is the important distinction:

- local post-build updates the package in your working copy
- CI also regenerates those DLLs, but only inside the runner workspace
- CI should normally validate and fail, not auto-commit back to your branch

That way, if you forget to rebuild before pushing, CI catches it immediately.

## What Was Removed

The old local playground, mock Unity runtime, duplicated runtime projects and editor-side duplicate project were removed from the repo because they were no longer the source of truth after the move to a real Unity package workflow.

## Current Direction

- Unity-only
- zero reflection in runtime
- `[Injek]` as the main public pseudo-constructor API
- generated `_Rizolver` and `_Fucktory`
- Unity scope tree and future Graph Toolkit tooling
