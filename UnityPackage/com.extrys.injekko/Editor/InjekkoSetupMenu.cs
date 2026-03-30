using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor
{
	public static class InjekkoSetupMenu
	{
		const string AnalyzerAssetPath = "Packages/com.extrys.injekko/Analyzers/InjekkoGen.dll";
		const string AnalyzerLabel = "RoslynAnalyzer";

		[MenuItem("Tools/Injekko/Validate Setup")]
		public static void ValidateSetup()
		{
			string[] messages =
			{
				ValidateAnalyzerPresence(),
				ValidateProjectGraph()
			};

			string report = string.Join(
				Environment.NewLine,
				messages.Where(static message => !string.IsNullOrWhiteSpace(message)));

			if (string.IsNullOrWhiteSpace(report))
				report = "Injekko setup looks good.";

			Debug.Log(report);
			EditorUtility.DisplayDialog("Injekko Setup", report, "OK");
		}

		[MenuItem("Tools/Injekko/Compile Graph Plans")]
		public static void CompileGraphPlans()
		{
			InjekScopeGraphCompiler.CompileAll();
			EditorUtility.DisplayDialog(
				"Injekko Graph Plans",
				$"Graph plans compiled to {InjekScopeGraphCompiler.GeneratedSourcePath}",
				"OK");
		}

		static string ValidateAnalyzerPresence()
		{
			PluginImporter importer = AssetImporter.GetAtPath(AnalyzerAssetPath) as PluginImporter;
			if (importer == null)
				return "Analyzer DLL not found at Packages/com.extrys.injekko/Analyzers/InjekkoGen.dll. Run tools/StageUnityAnalyzer.ps1 from the repo.";

			string[] labels = AssetDatabase.GetLabels(importer);
			bool hasAnalyzerLabel = labels.Any(static label => string.Equals(label, AnalyzerLabel, StringComparison.Ordinal));
			if (!hasAnalyzerLabel)
				return "Analyzer DLL is missing the RoslynAnalyzer label. Add that label in the Inspector so Unity loads it as a source generator.";

			if (importer.GetCompatibleWithAnyPlatform())
				return "Analyzer DLL still has Any Platform enabled. Disable normal plugin platforms in the Plugin Inspector.";

			return null;
		}

		static string ValidateProjectGraph()
		{
			var directProjectGraph = FindDirectProjectGraph();
			if (directProjectGraph == null)
				return $"No project bootstrap graph found. Create Resources/{InjekkoProjectConventions.ProjectPlanResourceName}.injekgraph.";

			return ValidateLoadedScenesHaveSceneScope();
		}

		static InjekCompiledScopePlan FindDirectProjectGraph()
		{
			foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(InjekCompiledScopePlan)} {InjekkoProjectConventions.ProjectPlanResourceName}"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrWhiteSpace(path))
					continue;
				if (!path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\Resources\\", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!string.Equals(Path.GetFileNameWithoutExtension(path), InjekkoProjectConventions.ProjectPlanResourceName, StringComparison.Ordinal))
					continue;

				var graph = AssetDatabase.LoadAssetAtPath<InjekCompiledScopePlan>(path);
				if (graph != null)
					return graph;
			}

			return null;
		}

		static string ValidateLoadedScenesHaveSceneScope()
		{
			for (int sceneIndex = 0; sceneIndex < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; sceneIndex++)
			{
				var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(sceneIndex);
				if (!scene.IsValid() || !scene.isLoaded)
					continue;

				bool hasSceneScope = scene.GetRootGameObjects()
					.SelectMany(static root => root.GetComponentsInChildren<SceneScope>(true))
					.Any();
				if (!hasSceneScope)
					return $"Loaded scene '{scene.name}' has no SceneScope. Injekko's main-path workflow now expects one SceneScope per gameplay scene.";

				var sceneScope = scene.GetRootGameObjects()
					.SelectMany(static root => root.GetComponentsInChildren<SceneScope>(true))
					.FirstOrDefault();
				if (sceneScope != null && sceneScope.GraphPlan == null)
					return $"Scene '{scene.name}' has a SceneScope but no scene graph assigned.";
			}

			return null;
		}
	}
}
