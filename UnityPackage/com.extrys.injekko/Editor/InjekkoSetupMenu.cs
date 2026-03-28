using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Injekko.Editor
{
	public static class InjekkoSetupMenu
	{
		const string AnalyzerAssetPath = "Packages/com.extrys.injekko/Analyzers/InjekkoGen.dll";
		const string AnalyzerLabel = "RoslynAnalyzer";
		const string ReportOutputPath = "Assets/Injekko/Generated/InjekkoGraphReport.txt";

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
			if (projectAssets.Length == 0)
				return "No InjekkoProjectAsset found. Create one and place it under a Resources folder as Resources/InjekkoProjectAsset.asset.";

			return null;
		}
	}
}
