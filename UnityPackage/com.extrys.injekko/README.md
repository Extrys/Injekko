# Injekko Unity Package

This is the first Unity-first package scaffold for Injekko. It already contains:

- `Runtime/`: attributes, bindings, scopes, activation and Unity integration.
- `Editor/`: setup validation and the first graph report tooling hooks.
- `Analyzers/`: the built `InjekkoGen.dll` source generator.

## Add It To A Unity Project

1. Open Package Manager.
2. Use `Add package from disk...`.
3. Pick `UnityPackage/com.extrys.injekko/package.json`.

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

Create an `InjekkoProjectAsset` and place it at:

- `Assets/Resources/InjekkoProjectAsset.asset`

That asset is loaded automatically before scene load and becomes the project root for scope setup.

## First Runtime Model

- Project scope is created automatically from `InjekkoProjectAsset`.
- Scene scopes are created automatically for loaded scenes.
- `InjekScopeAnchor` creates explicit subscopes on `GameObject`s when you need them.
- `GetInjekScope()` on `GameObject` and `Component` resolves through the scope registry instead of repeated `GetComponent` searches.

## First Tooling Hooks

The package currently includes:

- `Tools/Injekko/Validate Setup`
- `Tools/Injekko/Write Graph Report`

The graph report writes to:

- `Assets/Injekko/Generated/InjekkoGraphReport.txt`

That gives us a first editor-visible artifact from generated dependency metadata, ready to evolve later into Graph Toolkit views.
