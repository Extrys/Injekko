using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using Injekko.Unity;

namespace Injekko.Editor
{
	[InitializeOnLoad]
	internal static class InjekScopeGraphCompilationHooks
	{
		static bool isCompileQueued;

		static InjekScopeGraphCompilationHooks()
		{
			EditorSceneManager.sceneSaving += OnSceneSaving;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			CompilationPipeline.compilationFinished += OnCompilationFinished;
		}

		static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
		{
			foreach (var sceneScope in scene.GetRootGameObjects()
				.SelectMany(static root => root.GetComponentsInChildren<SceneScope>(true)))
			{
				InjekScopeGraphCompiler.RefreshSceneScopeCache(sceneScope);
			}

			QueueCompile();
		}

		static void OnPlayModeChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode)
				QueueCompile();
		}

		static void OnCompilationFinished(object _)
		{
			QueueCompile();
		}

		internal static void QueueCompile()
		{
			if (isCompileQueued)
				return;

			isCompileQueued = true;
			EditorApplication.delayCall += FlushQueuedCompile;
		}

		static void FlushQueuedCompile()
		{
			isCompileQueued = false;
			InjekScopeGraphCompiler.CompileAll();
		}

	}

	internal sealed class InjekScopeGraphBuildHook : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			InjekScopeGraphCompiler.CompileAll(refreshAssets: true);
		}
	}

	internal sealed class InjekScopeGraphPostprocessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			bool shouldCompile = importedAssets.Any(IsRelevantAssetPath)
				|| movedAssets.Any(IsRelevantAssetPath)
				|| deletedAssets.Any(IsRelevantAssetPath)
				|| movedFromAssetPaths.Any(IsRelevantAssetPath);
			if (!shouldCompile)
				return;

			InjekScopeGraphCompilationHooks.QueueCompile();
		}

		static bool IsRelevantAssetPath(string assetPath)
		{
			return assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
				|| assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
				|| assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
				|| assetPath.EndsWith(".injekgraph", StringComparison.OrdinalIgnoreCase);
		}
	}

}
