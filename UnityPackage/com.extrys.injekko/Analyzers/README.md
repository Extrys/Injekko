Run `tools/StageUnityAnalyzer.ps1` to place `InjekkoGen.dll`, `Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` here.

Inside Unity:
- add the `RoslynAnalyzer` label to `InjekkoGen.dll`
- disable normal plugin platforms in the Plugin Inspector

For Unity 6, the generator project is aligned to Roslyn `4.3.0`.
