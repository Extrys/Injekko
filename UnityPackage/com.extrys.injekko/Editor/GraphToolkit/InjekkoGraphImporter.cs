using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor.GraphToolkit
{
	[ScriptedImporter(1, InjekkoAuthoringGraph.AssetExtension)]
	internal sealed class InjekkoGraphImporter : ScriptedImporter
	{
		const string GraphIconPath = "Packages/com.extrys.injekko/Editor/Icons/InjekkoGraphIcon.png";

		public override void OnImportAsset(AssetImportContext ctx)
		{
			var graph = global::Unity.GraphToolkit.Editor.GraphDatabase.LoadGraphForImporter<InjekkoAuthoringGraph>(ctx.assetPath);
			if (graph == null)
			{
				Debug.LogError($"Failed to load Injekko graph asset: {ctx.assetPath}");
				return;
			}

			var compiledPlan = ScriptableObject.CreateInstance<InjekCompiledScopePlan>();
			string graphId = AssetDatabase.AssetPathToGUID(ctx.assetPath);
			string graphName = Path.GetFileNameWithoutExtension(ctx.assetPath);
			var compiledDefinitions = InjekkoGraphToolkitBridge.GetBindingDefinitions(ctx.assetPath)
				.Select(static definition => definition.ToCompiledDefinition())
				.ToArray();
			compiledPlan.SetImportedData(graphId, graphName, compiledDefinitions);

			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(GraphIconPath);
			ctx.AddObjectToAsset("CompiledScopePlan", compiledPlan, icon);
			ctx.SetMainObject(compiledPlan);
		}
	}
}
