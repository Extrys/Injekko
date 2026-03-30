using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekGeneratedRuntimeRegistry
	{
		static readonly Dictionary<string, Action<IInjekGraphReferenceHost, InjekScopeNode>> graphPlans = new(StringComparer.Ordinal);
		static Action<SceneScope> sceneScopeActivator;
		static Action<GameObject> hierarchyActivator;

		public static void RegisterGraphPlan(string graphId, Action<IInjekGraphReferenceHost, InjekScopeNode> applyPlan)
		{
			if (string.IsNullOrWhiteSpace(graphId) || applyPlan == null)
				return;

			graphPlans[graphId] = applyPlan;
		}

		public static void RegisterProjectGraphPlan(string graphId, Action<InjekkoProjectAsset, InjekScopeNode> applyPlan)
		{
			if (applyPlan == null)
				return;

			RegisterGraphPlan(graphId, (host, scope) =>
			{
				if (host is InjekkoProjectAsset projectAsset)
					applyPlan(projectAsset, scope);
			});
		}

		public static void RegisterSceneGraphPlan(string graphId, Action<SceneScope, InjekScopeNode> applyPlan)
		{
			if (applyPlan == null)
				return;

			RegisterGraphPlan(graphId, (host, scope) =>
			{
				if (host is SceneScope sceneScope)
					applyPlan(sceneScope, scope);
			});
		}

		public static void RegisterGameObjectGraphPlan(string graphId, Action<GameObjectScope, InjekScopeNode> applyPlan)
		{
			if (applyPlan == null)
				return;

			RegisterGraphPlan(graphId, (host, scope) =>
			{
				if (host is GameObjectScope gameObjectScope)
					applyPlan(gameObjectScope, scope);
			});
		}

		public static void RegisterSceneActivation(Action<SceneScope> activateSceneScope, Action<GameObject> activateHierarchy)
		{
			sceneScopeActivator = activateSceneScope;
			hierarchyActivator = activateHierarchy;
		}

		public static bool TryApplyGraphPlan(IInjekGraphReferenceHost host, InjekScopeNode scope)
		{
			if (host == null || scope == null)
				return false;

			if (!graphPlans.TryGetValue(host.GraphId, out var plan))
				return false;

			plan(host, scope);
			return true;
		}

		public static bool TryApplyProjectGraphPlan(InjekkoProjectAsset host, InjekScopeNode scope)
			=> TryApplyGraphPlan(host, scope);

		public static bool TryApplySceneGraphPlan(SceneScope host, InjekScopeNode scope)
			=> TryApplyGraphPlan(host, scope);

		public static bool TryApplyGameObjectGraphPlan(GameObjectScope host, InjekScopeNode scope)
			=> TryApplyGraphPlan(host, scope);

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
