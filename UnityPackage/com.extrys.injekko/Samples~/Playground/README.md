# Injekko Playground Sample

This sample is the package-local playground for validating the current Unity-first workflow.

## What It Demonstrates


- `SceneScope` bindings via its included graph
- scene `MonoBehaviour` auto-activation through generated `_Rizolver.Activate(...)`
- generated `*_Fucktory` usage for runtime-created components

## Doesnt demonstrates

- `ProjectScope` bindings, because the project graph is resource folder and single per project and its meant to not being endirten by other packages, currently the scenegraph workflow is enough for the sample, as it adds to the project graph

## Suggested Setup

1. Import this sample from the Package Manager.
2. Open the DemonstrationScene
3. Enter play mode and watch the logs.

## Expected Behavior

- `TestPlayerInputInjekko` should log from its `[Injek]` method when the scene loads.
- Press `Space` and `MySuperCustomComponent_Fucktory` should create a component with dependencies already injected.
