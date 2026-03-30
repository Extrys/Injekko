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
