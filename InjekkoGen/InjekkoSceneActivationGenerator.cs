using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Injekko.Codegen
{
	[Generator]
	public sealed class InjekkoSceneActivationGenerator : IIncrementalGenerator
	{
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			var injekMethods = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorDiscovery.IsCandidateInjekMethod(node),
					static (generatorContext, _) => InjekkoGeneratorDiscovery.GetAnnotatedMethod(generatorContext))
				.Where(static methodSymbol => methodSymbol != null)
				.Select(static (methodSymbol, _) => methodSymbol!);

			var fucktoryTargets = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => InjekkoGeneratorDiscovery.IsCandidateFucktoryTarget(node),
					static (generatorContext, _) => InjekkoGeneratorDiscovery.GetFucktoryTarget(generatorContext))
				.Where(static target => target != null)
				.Select(static (target, _) => target!);

			var compilationAndInjekMethods = context.CompilationProvider.Combine(injekMethods.Collect());
			var fullInput = compilationAndInjekMethods.Combine(fucktoryTargets.Collect());

			context.RegisterSourceOutput(fullInput, static (productionContext, source) =>
			{
				Execute(productionContext, source.Left.Left, source.Left.Right, source.Right);
			});
		}

		static void Execute(
			SourceProductionContext context,
			Compilation compilation,
			ImmutableArray<IMethodSymbol> candidateMethods,
			ImmutableArray<FucktoryTargetModel> candidateFucktories)
		{
			if (candidateMethods.IsDefaultOrEmpty)
				return;

			var methods = InjekkoGeneratorDiscovery.DistinctMethods(candidateMethods);
			var fucktories = InjekkoGeneratorDiscovery.DistinctFucktories(candidateFucktories);
			if (methods.Count == 0)
				return;

			if (compilation.GetTypeByMetadataName("UnityEngine.Component") == null)
				return;

			StringBuilder sourceBuilder = new();
			sourceBuilder.AppendLine("namespace Injekko.Unity");
			sourceBuilder.AppendLine("{");
			sourceBuilder.AppendLine("\tinternal static class InjekkoSceneActivationBootstrap");
			sourceBuilder.AppendLine("\t{");
			sourceBuilder.AppendLine("\t\t[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
			sourceBuilder.AppendLine("\t\tstatic void Register()");
			sourceBuilder.AppendLine("\t\t{");
			sourceBuilder.AppendLine("\t\t\tglobal::Injekko.Unity.InjekGeneratedRuntimeRegistry.RegisterSceneActivation(ActivateSceneScope, ActivateHierarchy);");
			sourceBuilder.AppendLine("\t\t\tglobal::UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;");
			sourceBuilder.AppendLine("\t\t\tglobal::UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;");
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine("\t\t[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]");
			sourceBuilder.AppendLine("\t\tstatic void ActivateInitialScene()");
			sourceBuilder.AppendLine("\t\t{");
			sourceBuilder.AppendLine("\t\t\tvar activeScene = global::UnityEngine.SceneManagement.SceneManager.GetActiveScene();");
			sourceBuilder.AppendLine("\t\t\tif (global::Injekko.Unity.InjekScopeRegistry.TryGetSceneScope(activeScene, out var explicitSceneScope) && explicitSceneScope != null && explicitSceneScope.IsBootstrapComplete)");
			sourceBuilder.AppendLine("\t\t\t\treturn;");
			sourceBuilder.AppendLine("\t\t\tActivateScene(activeScene);");
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine("\t\tstatic void OnSceneLoaded(global::UnityEngine.SceneManagement.Scene scene, global::UnityEngine.SceneManagement.LoadSceneMode mode)");
			sourceBuilder.AppendLine("\t\t{");
			sourceBuilder.AppendLine("\t\t\tif (global::Injekko.Unity.InjekScopeRegistry.TryGetSceneScope(scene, out var explicitSceneScope) && explicitSceneScope != null && explicitSceneScope.IsBootstrapComplete)");
			sourceBuilder.AppendLine("\t\t\t\treturn;");
			sourceBuilder.AppendLine("\t\t\tActivateScene(scene);");
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine("\t\tinternal static void ActivateSceneScope(global::Injekko.Unity.SceneScope sceneScope)");
			sourceBuilder.AppendLine("\t\t{");
			sourceBuilder.AppendLine("\t\t\tif (sceneScope == null)");
			sourceBuilder.AppendLine("\t\t\t\tthrow new global::System.ArgumentNullException(nameof(sceneScope));");
			AppendSceneScopeActivationLoops(sourceBuilder, context, compilation, methods, fucktories);
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine("\t\tinternal static void ActivateHierarchy(global::UnityEngine.GameObject root)");
			sourceBuilder.AppendLine("\t\t{");
			sourceBuilder.AppendLine("\t\t\tif (root == null)");
			sourceBuilder.AppendLine("\t\t\t\tthrow new global::System.ArgumentNullException(nameof(root));");
			AppendHierarchyActivationLoops(sourceBuilder, context, compilation, methods, fucktories, "\t\t\t", "root.GetComponentsInChildren");
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine();
			sourceBuilder.AppendLine("\t\tstatic void ActivateScene(global::UnityEngine.SceneManagement.Scene scene)");
			sourceBuilder.AppendLine("\t\t{");
			AppendSceneActivationLoops(sourceBuilder, context, compilation, methods, fucktories);
			sourceBuilder.AppendLine("\t\t}");
			sourceBuilder.AppendLine("\t}");
			sourceBuilder.AppendLine("}");

			context.AddSource("InjekkoSceneActivationGenerated.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
		}

		static void AppendHierarchyActivationLoops(StringBuilder sourceBuilder, SourceProductionContext context, Compilation compilation, System.Collections.Generic.List<IMethodSymbol> methods, System.Collections.Generic.List<FucktoryTargetModel> fucktories, string indent, string hierarchyFetchExpression)
		{
			foreach (var method in methods)
			{
				if (!ShouldGenerateActivationForMethod(context, compilation, method, fucktories))
					continue;

				string componentTypeName = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"{indent}var {method.ContainingType.Name}_hierarchyInstances = {hierarchyFetchExpression}<{componentTypeName}>(true);");
				sourceBuilder.AppendLine($"{indent}foreach (var instance in {method.ContainingType.Name}_hierarchyInstances)");
				sourceBuilder.AppendLine($"{indent}{{");
				sourceBuilder.AppendLine($"{indent}\tif (instance == null)");
				sourceBuilder.AppendLine($"{indent}\t\tcontinue;");
				sourceBuilder.AppendLine($"{indent}\tglobal::{InjekkoGeneratorNaming.TrimGlobal(componentTypeName)}_Rizolver.Activate(instance, global::Injekko.Unity.InjekScopeRegistry.GetScope(instance));");
				sourceBuilder.AppendLine($"{indent}}}");
			}
		}

		static void AppendSceneScopeActivationLoops(StringBuilder sourceBuilder, SourceProductionContext context, Compilation compilation, System.Collections.Generic.List<IMethodSymbol> methods, System.Collections.Generic.List<FucktoryTargetModel> fucktories)
		{
			foreach (var method in methods)
			{
				if (!ShouldGenerateActivationForMethod(context, compilation, method, fucktories))
					continue;

				string componentTypeName = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"\t\t\tforeach (var cachedComponent in sceneScope.CachedInjectables)");
				sourceBuilder.AppendLine("\t\t\t{");
				sourceBuilder.AppendLine($"\t\t\t\tif (cachedComponent is not {componentTypeName} instance)");
				sourceBuilder.AppendLine("\t\t\t\t\tcontinue;");
				sourceBuilder.AppendLine($"\t\t\t\tglobal::{InjekkoGeneratorNaming.TrimGlobal(componentTypeName)}_Rizolver.Activate(instance, global::Injekko.Unity.InjekScopeRegistry.GetScope(instance));");
				sourceBuilder.AppendLine("\t\t\t}");
			}
		}

		static void AppendSceneActivationLoops(StringBuilder sourceBuilder, SourceProductionContext context, Compilation compilation, System.Collections.Generic.List<IMethodSymbol> methods, System.Collections.Generic.List<FucktoryTargetModel> fucktories)
		{
			foreach (var method in methods)
			{
				if (!ShouldGenerateActivationForMethod(context, compilation, method, fucktories))
					continue;

				string componentTypeName = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				sourceBuilder.AppendLine($"\t\t\tvar {method.ContainingType.Name}_instances = global::UnityEngine.Object.FindObjectsByType<{componentTypeName}>(global::UnityEngine.FindObjectsInactive.Include, global::UnityEngine.FindObjectsSortMode.None);");
				sourceBuilder.AppendLine($"\t\t\tforeach (var instance in {method.ContainingType.Name}_instances)");
				sourceBuilder.AppendLine("\t\t\t{");
				sourceBuilder.AppendLine("\t\t\t\tif (instance == null || instance.gameObject.scene != scene)");
				sourceBuilder.AppendLine("\t\t\t\t\tcontinue;");
				sourceBuilder.AppendLine("\t\t\t\tglobal::" + InjekkoGeneratorNaming.TrimGlobal(componentTypeName) + "_Rizolver.Activate(instance, global::Injekko.Unity.InjekScopeRegistry.GetScope(instance));");
				sourceBuilder.AppendLine("\t\t\t}");
			}
		}

		static bool ShouldGenerateActivationForMethod(SourceProductionContext context, Compilation compilation, IMethodSymbol method, System.Collections.Generic.List<FucktoryTargetModel> fucktories)
		{
			if (!InjekkoInjekSupport.TryValidateInjekMethod(context, method))
				return false;

			if (!InjekkoFucktoryGeneration.IsComponentType(compilation, method.ContainingType))
				return false;

			var associatedFucktory = InjekkoFucktoryGeneration.FindFucktoryForTarget(fucktories, method.ContainingType);
			if (associatedFucktory != null
				&& associatedFucktory.RuntimeArgumentTypes.Length > 0
				&& InjekkoFucktoryGeneration.MatchesTrailingRuntimeArguments(method, associatedFucktory.RuntimeArgumentTypes))
			{
				return false;
			}

			return true;
		}
	}
}
