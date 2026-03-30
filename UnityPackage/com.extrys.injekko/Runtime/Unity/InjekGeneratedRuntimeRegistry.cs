using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekGeneratedRuntimeRegistry
	{
		static readonly Dictionary<string, Action<InjekkoProjectAsset, InjekScopeNode>> projectGraphPlans = new(StringComparer.Ordinal);
		static readonly Dictionary<string, Action<SceneScope, InjekScopeNode>> sceneGraphPlans = new(StringComparer.Ordinal);
		static readonly Dictionary<string, Action<GameObjectScope, InjekScopeNode>> gameObjectGraphPlans = new(StringComparer.Ordinal);
		static Action<SceneScope> sceneScopeActivator;
		static Action<GameObject> hierarchyActivator;

		public static void RegisterProjectGraphPlan(string graphId, Action<InjekkoProjectAsset, InjekScopeNode> applyPlan)
		{
			if (string.IsNullOrWhiteSpace(graphId) || applyPlan == null)
				return;

			projectGraphPlans[graphId] = applyPlan;
		}

		public static void RegisterSceneGraphPlan(string graphId, Action<SceneScope, InjekScopeNode> applyPlan)
		{
			if (string.IsNullOrWhiteSpace(graphId) || applyPlan == null)
				return;

			sceneGraphPlans[graphId] = applyPlan;
		}

		public static void RegisterGameObjectGraphPlan(string graphId, Action<GameObjectScope, InjekScopeNode> applyPlan)
		{
			if (string.IsNullOrWhiteSpace(graphId) || applyPlan == null)
				return;

			gameObjectGraphPlans[graphId] = applyPlan;
		}

		public static void RegisterSceneActivation(Action<SceneScope> activateSceneScope, Action<GameObject> activateHierarchy)
		{
			sceneScopeActivator = activateSceneScope;
			hierarchyActivator = activateHierarchy;
		}

		public static bool TryApplyProjectGraphPlan(InjekkoProjectAsset projectAsset, InjekScopeNode scope)
		{
			if (projectAsset == null || scope == null)
				return false;

			if (!projectGraphPlans.TryGetValue(projectAsset.GraphId, out var plan))
				return false;

			plan(projectAsset, scope);
			return true;
		}

		public static bool TryApplySceneGraphPlan(SceneScope sceneScope, InjekScopeNode scope)
		{
			if (sceneScope == null || scope == null)
				return false;

			if (!sceneGraphPlans.TryGetValue(sceneScope.GraphId, out var plan))
				return false;

			plan(sceneScope, scope);
			return true;
		}

		public static bool TryApplyGameObjectGraphPlan(GameObjectScope gameObjectScope, InjekScopeNode scope)
		{
			if (gameObjectScope == null || scope == null)
				return false;

			if (!gameObjectGraphPlans.TryGetValue(gameObjectScope.GraphId, out var plan))
				return false;

			plan(gameObjectScope, scope);
			return true;
		}

		public static bool TryActivateSceneScope(SceneScope sceneScope)
		{
			if (sceneScopeActivator == null || sceneScope == null)
				return false;

			sceneScopeActivator(sceneScope);
			return true;
		}

		public static void ActivateHierarchy(GameObject root)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));

			if (hierarchyActivator == null)
				throw new InjekException("No generated hierarchy activator has been registered for Injekko.");

			hierarchyActivator(root);
		}

		public static bool HasSceneActivation => sceneScopeActivator != null;
	}
}
