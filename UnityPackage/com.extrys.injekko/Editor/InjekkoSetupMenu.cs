using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Injekko;
using Injekko.Unity;

namespace Injekko.Editor
{
	public static class InjekkoSetupMenu
	{
		const string AnalyzerAssetPath = "Packages/com.extrys.injekko/Analyzers/InjekkoGen.dll";
		const string AnalyzerLabel = "RoslynAnalyzer";
		const string ReportOutputPath = "Assets/Injekko/Generated/InjekkoGraphReport.txt";
		const string ProjectResourceName = "InjekkoProjectAsset";

		[MenuItem("Tools/Injekko/Validate Setup")]
		public static void ValidateSetup()
		{
			string[] messages =
			{
				ValidateAnalyzerPresence(),
				ValidateProjectAsset()
			};

			string report = string.Join(
				Environment.NewLine,
				messages.Where(static message => !string.IsNullOrWhiteSpace(message)));

			if (string.IsNullOrWhiteSpace(report))
				report = "Injekko setup looks good.";

			Debug.Log(report);
			EditorUtility.DisplayDialog("Injekko Setup", report, "OK");
		}

		[MenuItem("Tools/Injekko/Write Graph Report")]
		public static void WriteGraphReport()
		{
			Type metadataFactoryType = AppDomain.CurrentDomain
				.GetAssemblies()
				.Select(static assembly => assembly.GetType("Injekko_GraphMetadata", throwOnError: false))
				.FirstOrDefault(static type => type != null);

			if (metadataFactoryType == null)
			{
				EditorUtility.DisplayDialog(
					"Injekko Graph Report",
					"Generated graph metadata was not found. Make sure the analyzer is imported correctly and the consumer assembly has compiled.",
					"OK");
				return;
			}

			MethodInfo createMethod = metadataFactoryType.GetMethod(
				"Create",
				BindingFlags.Public | BindingFlags.Static);

			if (createMethod == null)
			{
				EditorUtility.DisplayDialog(
					"Injekko Graph Report",
					"The generated metadata type exists, but it does not expose a public static Create() method.",
					"OK");
				return;
			}

			if (createMethod.Invoke(null, null) is not InjekGraphMetadata metadata)
			{
				EditorUtility.DisplayDialog(
					"Injekko Graph Report",
					"Failed to create graph metadata from the generated factory.",
					"OK");
				return;
			}

			string report = InjekkoGraphReportBuilder.BuildReport(metadata);
			string fullDirectoryPath = Path.GetDirectoryName(ReportOutputPath);
			if (!string.IsNullOrWhiteSpace(fullDirectoryPath))
				Directory.CreateDirectory(fullDirectoryPath);

			File.WriteAllText(ReportOutputPath, report);
			AssetDatabase.Refresh();

			Debug.Log($"Injekko graph report written to {ReportOutputPath}");
			EditorUtility.DisplayDialog(
				"Injekko Graph Report",
				$"Graph report written to {ReportOutputPath}",
				"OK");
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

		static string ValidateProjectAsset()
		{
			string[] projectAssets = AssetDatabase.FindAssets("t:InjekkoProjectAsset");
			var directProjectGraph = FindDirectProjectGraph();
			if (projectAssets.Length == 0 && directProjectGraph == null)
				return "No project bootstrap graph found. Create Resources/InjekkoProjectAsset.injekgraph, or use the legacy Resources/InjekkoProjectAsset.asset wrapper.";

			var projectAsset = projectAssets.Length > 0
				? AssetDatabase.LoadAssetAtPath<InjekkoProjectAsset>(AssetDatabase.GUIDToAssetPath(projectAssets[0]))
				: null;
			if (projectAsset != null && projectAsset.GraphPlan == null && directProjectGraph == null)
				return "InjekkoProjectAsset is missing its ProjectScope graph. Assign a .injekgraph asset to drive project bindings.";

			return ValidateLoadedScenesHaveSceneScope();
		}

		static InjekCompiledScopePlan FindDirectProjectGraph()
		{
			foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(InjekCompiledScopePlan)} {ProjectResourceName}"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrWhiteSpace(path))
					continue;
				if (!path.Contains("/Resources/", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\Resources\\", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!string.Equals(Path.GetFileNameWithoutExtension(path), ProjectResourceName, StringComparison.Ordinal))
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
