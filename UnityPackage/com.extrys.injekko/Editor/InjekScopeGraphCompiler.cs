using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor
{
	internal static class InjekScopeGraphCompiler
	{
		internal const string GeneratedSourcePath = InjekGraphPlanCodeGenerator.GeneratedSourcePath;

		public static void CompileAll(bool refreshAssets = true)
		{
			foreach (var sceneScope in FindLoadedSceneScopes())
				RefreshSceneScopeCache(sceneScope);

			foreach (var gameObjectScope in FindLoadedGameObjectScopes())
				RefreshGameObjectScopeBindings(gameObjectScope);

			InjekGraphPlanCodeGenerator.GenerateAndWriteAll(refreshAssets);
		}

		public static void RefreshSceneScopeCache(SceneScope sceneScope)
		{
			if (sceneScope == null)
				return;

			InjekScopeHostBindingSynchronizer.RefreshSceneScopeBindings(sceneScope);
			InjekSceneScopeCacheBuilder.RefreshSceneScopeCaches(sceneScope);
		}

		public static void RefreshGameObjectScopeBindings(GameObjectScope gameObjectScope)
		{
			InjekScopeHostBindingSynchronizer.RefreshGameObjectScopeBindings(gameObjectScope);
		}

		static IReadOnlyList<SceneScope> FindLoadedSceneScopes()
		{
			return UnityEngine.Object.FindObjectsByType<SceneScope>(FindObjectsInactive.Include)
				.Where(static scope => scope != null && scope.gameObject.scene.IsValid() && scope.gameObject.scene.isLoaded)
				.OrderBy(static scope => scope.gameObject.scene.path, StringComparer.Ordinal)
				.ThenBy(static scope => GetHierarchyPath(scope.transform), StringComparer.Ordinal)
				.ToArray();
		}

		static IReadOnlyList<GameObjectScope> FindLoadedGameObjectScopes()
		{
			return UnityEngine.Object.FindObjectsByType<GameObjectScope>(FindObjectsInactive.Include)
				.Where(static scope => scope != null && scope.gameObject.scene.IsValid() && scope.gameObject.scene.isLoaded)
				.OrderBy(static scope => scope.gameObject.scene.path, StringComparer.Ordinal)
				.ThenBy(static scope => GetHierarchyPath(scope.transform), StringComparer.Ordinal)
				.ToArray();
		}

		static string GetHierarchyPath(Transform transform)
		{
			string path = transform.name;
			for (var current = transform.parent; current != null; current = current.parent)
				path = current.name + "/" + path;
			return path;
		}
	}
}
