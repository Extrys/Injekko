# Injekko Playground Sample

This sample is the package-local playground for validating the current Unity-first workflow.

## What It Demonstrates

- `ProjectScope` bindings via `PlaygroundProjectInstaller`
- `SceneScope` bindings via `PlaygroundSceneInstaller`
- scene `MonoBehaviour` auto-activation through generated `_Rizolver.Activate(...)`
- generated `*_Fucktory` usage for runtime-created components

## Suggested Setup

1. Import this sample from the Package Manager.
2. Create `Assets/Resources/InjekkoProjectAsset.asset`.
3. Create a `PlaygroundProjectInstaller` asset and assign it to the project asset.
4. Create a `MyVFXDB` asset and assign it to the project installer.
5. In a scene, add one `SceneScope` component somewhere.
6. Create a `PlaygroundSceneInstaller` asset and assign it to that `SceneScope`.
7. Create another `MyVFXDB` asset for the scene override and assign it to the scene installer.
8. Put `TestPlayerInputInjekko` and `SceneScopeProbe` on scene objects.
9. Enter play mode and watch the logs.

## Expected Behavior

- `TestPlayerInputInjekko` should log from its `[Injek]` method when the scene loads.
- `SceneScopeProbe` should receive the scene-only binding and the overridden `MyVFXDB`.
- Press `Space` and `MySuperCustomComponent_Fucktory` should create a component with dependencies already injected.
