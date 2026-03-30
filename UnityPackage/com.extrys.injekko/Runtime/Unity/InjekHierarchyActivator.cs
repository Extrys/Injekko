using System;
using UnityEngine;

namespace Injekko.Unity
{
	public static class InjekHierarchyActivator
	{
		static GameObject stagingRoot;

		public static T InstantiatePrefab<T>(T prefab, IInjekScope parentScope, Action<GameObject> activateHierarchy) where T : Component
		{
			if (prefab == null)
				throw new ArgumentNullException(nameof(prefab));
			if (parentScope == null)
				throw new ArgumentNullException(nameof(parentScope));
			if (activateHierarchy == null)
				throw new ArgumentNullException(nameof(activateHierarchy));

			Transform stagingParent = GetOrCreateStagingRoot().transform;
			T instance = UnityEngine.Object.Instantiate(prefab, stagingParent);

			try
			{
				PrepareHierarchy(instance.gameObject, parentScope);
				activateHierarchy(instance.gameObject);
				instance.transform.SetParent(null, true);
				return instance;
			}
			catch
			{
				if (instance != null)
					UnityEngine.Object.Destroy(instance.gameObject);
				throw;
			}
		}

		public static void PrepareHierarchy(GameObject root, IInjekScope parentScope)
		{
			if (root == null)
				throw new ArgumentNullException(nameof(root));
			if (parentScope == null)
				throw new ArgumentNullException(nameof(parentScope));

			PrepareTransform(root.transform, parentScope);
		}

		static void PrepareTransform(Transform current, IInjekScope parentScope)
		{
			IInjekScope currentScope = parentScope;

			GameObjectScope gameObjectScope = current.GetComponent<GameObjectScope>();
			if (gameObjectScope != null)
			{
				InjekScopeNode scopeNode = InjekScopeRegistry.EnsureGameObjectScope(current.gameObject, parentScope, gameObjectScope.LegacyInstallers);
				gameObjectScope.AssignScope(scopeNode);
				InjekGeneratedRuntimeRegistry.TryApplyGraphPlan(gameObjectScope, scopeNode);
				currentScope = scopeNode;
			}

			for (int childIndex = 0; childIndex < current.childCount; childIndex++)
				PrepareTransform(current.GetChild(childIndex), currentScope);
		}

		static GameObject GetOrCreateStagingRoot()
		{
			if (stagingRoot != null)
				return stagingRoot;

			stagingRoot = new GameObject("Injekko_PrefabStagingRoot");
			stagingRoot.SetActive(false);
			stagingRoot.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(stagingRoot);
			return stagingRoot;
		}
	}
}
