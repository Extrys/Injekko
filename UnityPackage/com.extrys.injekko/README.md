# Injekko Unity Package

This is the first Unity-first package scaffold for Injekko. It already contains:

- `Runtime/`: attributes, bindings, scopes, activation and Unity integration.
- `Editor/`: setup validation, graph-plan compilation and graph authoring hooks.
- `Analyzers/`: the built `InjekkoGen.dll` source generator.

If you want the fastest code-reading map through the current graph-first runtime, start with `ARCHITECTURE.md`.

## Add It To A Unity Project

1. Open Package Manager.
2. Use `Add package from disk...`.
3. Pick `UnityPackage/com.extrys.injekko/package.json`.

The package also exposes importable samples through the Package Manager `Samples` section.

## Prepare The Analyzer

The repo includes `tools/StageUnityAnalyzer.ps1`, which builds the generator and stages only the analyzer plus the Roslyn compiler DLLs Unity actually needs into `Analyzers/`.

After importing the package in Unity:

1. Run `tools/StageUnityAnalyzer.ps1` from the repo root.
2. Select `Packages/com.extrys.injekko/Analyzers/InjekkoGen.dll`.
3. Add the `RoslynAnalyzer` label.
4. Disable `Any Platform` and the normal runtime plugin platforms in the Plugin Inspector.

If Unity reports missing `Microsoft.CodeAnalysis` references, re-run the staging script and confirm `Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` are present beside `InjekkoGen.dll`.

You can then run `Tools/Injekko/Validate Setup` inside Unity to verify the package is wired correctly.

## Project Bootstrap

Create your project graph directly at:

- `Assets/Resources/ProjectPlan.injekgraph`

That graph is loaded automatically before scene load and becomes the project root for scope setup. Because project graphs never need scene references, their asset references live directly on the graph itself.

Each gameplay scene should also contain one `SceneScope` component with its own `.injekgraph` asset.

## First Runtime Model

- Project scope is created automatically from `Resources/ProjectPlan.injekgraph`.
- Each gameplay scene is expected to declare one explicit `SceneScope` component.
- `ProjectScope` and `SceneScope` bindings are now driven by graph assets that compile to generated binding plans.
- `GameObjectScope` creates explicit subscopes on `GameObject`s when you need them.
- `GetInjekScope()` on `GameObject` and `Component` resolves through the scope registry instead of repeated `GetComponent` searches.
- Scene `MonoBehaviour`s with `[Injek]` are activated from the `SceneScope` cache in the main path, without runtime reflection.
- Prefab-backed `Fucktory` creation is now the recommended path for Unity gameplay objects.
- `AddComponent` creation is still supported, but it is the secondary path and does not carry the same strong lifecycle guarantees.

## Graph-Driven Authoring

- `.injekgraph` is the reusable authoring asset for `ProjectScope` and `SceneScope`, and imports into a draggable compiled runtime plan.
- Graph nodes compile to generated binding-plan code in `Assets/Injekko/Generated/InjekkoGraphPlans.Generated.cs`.
- `SceneScope` stores concrete scene/object references for graph slots, Timeline-style.
- The first supported graph path is explicit instance binding through reference slots.

## Prefab-Backed Fucktories

For a generated component factory, bind the prefab from your installer through the generated helper:

```csharp
MyEnemy_Fucktory.BindPrefab(builder, myEnemyPrefab);
```

When that factory creates an instance, Injekko will:

- instantiate the prefab hierarchy
- register any `GameObjectScope` components in that hierarchy top-down
- activate all `[Injek]` components in the instantiated tree

## First Tooling Hooks

The package currently includes:

- `Tools/Injekko/Validate Setup`
- `Tools/Injekko/Compile Graph Plans`

That gives us the first essential editor hooks for the graph-driven workflow, alongside scene cache generation and generated graph-plan compilation.
