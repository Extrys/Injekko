using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekScopeRegistry
	{
		static InjekkoProjectAsset projectAsset;
		static InjekScopeNode projectScope;
		static readonly Dictionary<ulong, InjekScopeNode> sceneScopes = new();
		static readonly Dictionary<GameObject, InjekScopeNode> anchoredScopes = new();

		public static void Configure(InjekkoProjectAsset asset)
		{
			projectAsset = asset;
			projectScope = null;
			sceneScopes.Clear();
			anchoredScopes.Clear();
		}

		public static InjekScopeNode GetProjectScope()
		{
			if (projectScope != null)
				return projectScope;

			projectScope = new InjekScopeNode(projectAsset != null ? projectAsset.ProjectName : "InjekkoProject", InjekScopeKind.Project, projectAsset);
			if (projectAsset != null)
				projectScope.Install(projectAsset.GetProjectInstallers());
			return projectScope;
		}

		public static InjekScopeNode EnsureSceneScope(Scene scene)
		{
			ulong sceneKey = GetSceneKey(scene);
			if (sceneScopes.TryGetValue(sceneKey, out var scope))
				return scope;

			scope = new InjekScopeNode(scene.name, InjekScopeKind.Scene, scene, GetProjectScope());
			if (projectAsset != null)
				scope.Install(projectAsset.GetSceneInstallers(scene));
			sceneScopes[sceneKey] = scope;
			return scope;
		}

		public static InjekScopeNode EnsureSubscope(GameObject gameObject, IEnumerable<InjekInstallerAsset> installers = null)
		{
			if (anchoredScopes.TryGetValue(gameObject, out var scope))
				return scope;

			var parentScope = ResolveParentScope(gameObject);
			scope = new InjekScopeNode(gameObject.name, InjekScopeKind.GameObject, gameObject, parentScope);
			if (installers != null)
				scope.Install(installers);
			anchoredScopes[gameObject] = scope;
			return scope;
		}

		public static IInjekScope GetScope(GameObject gameObject) => GetScopeNode(gameObject);
		public static IInjekScope GetScope(Component component) => GetScopeNode(component);

		public static InjekScopeNode GetScopeNode(Component component)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			return GetScopeNode(component.gameObject);
		}

		public static InjekScopeNode GetScopeNode(GameObject gameObject)
		{
			if (gameObject == null)
				throw new ArgumentNullException(nameof(gameObject));

			var current = gameObject.transform;
			while (current != null)
			{
				if (anchoredScopes.TryGetValue(current.gameObject, out var anchoredScope))
					return anchoredScope;
				current = current.parent;
			}

			return EnsureSceneScope(gameObject.scene);
		}

		public static IEnumerable<InjekScopeNode> EnumerateScopes()
		{
			if (projectScope != null)
				yield return projectScope;
			foreach (var scope in sceneScopes.Values)
				yield return scope;
			foreach (var scope in anchoredScopes.Values)
				yield return scope;
		}

		public static void ReleaseScene(Scene scene)
		{
			sceneScopes.Remove(GetSceneKey(scene));

			List<GameObject> toRemove = new();
			foreach (var entry in anchoredScopes)
			{
				if (entry.Key != null && entry.Key.scene == scene)
					toRemove.Add(entry.Key);
			}

			foreach (var gameObject in toRemove)
				anchoredScopes.Remove(gameObject);
		}

		static InjekScopeNode ResolveParentScope(GameObject gameObject)
		{
			var current = gameObject.transform.parent;
			while (current != null)
			{
				if (anchoredScopes.TryGetValue(current.gameObject, out var scope))
					return scope;
				current = current.parent;
			}

			return EnsureSceneScope(gameObject.scene);
		}

		static ulong GetSceneKey(Scene scene) => scene.handle.GetRawData();
	}
}
