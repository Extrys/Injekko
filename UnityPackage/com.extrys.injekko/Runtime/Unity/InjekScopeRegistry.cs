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
		static readonly Dictionary<ulong, SceneScopeEntry> sceneScopes = new();
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
			SceneScopeEntry entry = GetOrCreateSceneEntry(scene, sceneKey);
			TryAutoRegisterSceneScope(scene, entry);
			EnsureSceneInstallers(entry);
			return entry.Scope;
		}

		public static InjekScopeNode RegisterSceneScope(SceneScope sceneScope)
		{
			if (sceneScope == null)
				throw new ArgumentNullException(nameof(sceneScope));

			Scene scene = sceneScope.gameObject.scene;
			ulong sceneKey = GetSceneKey(scene);
			SceneScopeEntry entry = GetOrCreateSceneEntry(scene, sceneKey);

			if (entry.SceneScopeComponent != null && entry.SceneScopeComponent != sceneScope)
				throw new InjekException($"Only one SceneScope is allowed in scene '{scene.name}'.");

			entry.SceneScopeComponent = sceneScope;
			sceneScope.AssignScope(entry.Scope);
			EnsureSceneInstallers(entry);
			return entry.Scope;
		}

		public static InjekScopeNode EnsureGameObjectScope(GameObject gameObject, IEnumerable<InjekInstallerAsset> installers = null)
			=> EnsureSubscope(gameObject, installers);

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
			foreach (var entry in sceneScopes.Values)
				yield return entry.Scope;
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

		static SceneScopeEntry GetOrCreateSceneEntry(Scene scene, ulong sceneKey)
		{
			if (sceneScopes.TryGetValue(sceneKey, out var existingEntry))
				return existingEntry;

			SceneScopeEntry newEntry = new(new InjekScopeNode(scene.name, InjekScopeKind.Scene, scene, GetProjectScope()));
			sceneScopes[sceneKey] = newEntry;
			return newEntry;
		}

		static void TryAutoRegisterSceneScope(Scene scene, SceneScopeEntry entry)
		{
			if (entry.SceneScopeComponent != null || entry.HasScannedForSceneScope)
				return;

			entry.HasScannedForSceneScope = true;

			SceneScope foundSceneScope = null;
			foreach (GameObject rootObject in scene.GetRootGameObjects())
			{
				SceneScope[] scopes = rootObject.GetComponentsInChildren<SceneScope>(true);
				foreach (SceneScope scope in scopes)
				{
					if (foundSceneScope != null && foundSceneScope != scope)
						throw new InjekException($"Only one SceneScope is allowed in scene '{scene.name}'.");

					foundSceneScope = scope;
				}
			}

			if (foundSceneScope == null)
				return;

			entry.SceneScopeComponent = foundSceneScope;
			foundSceneScope.AssignScope(entry.Scope);
		}

		static void EnsureSceneInstallers(SceneScopeEntry entry)
		{
			if (entry.SceneScopeComponent == null || entry.HasInstalledSceneInstallers)
				return;

			entry.Scope.Install(entry.SceneScopeComponent.Installers);
			entry.HasInstalledSceneInstallers = true;
		}

		static ulong GetSceneKey(Scene scene) => scene.handle.GetRawData();

		sealed class SceneScopeEntry
		{
			public SceneScopeEntry(InjekScopeNode scope)
			{
				Scope = scope;
			}

			public InjekScopeNode Scope { get; }
			public SceneScope SceneScopeComponent { get; set; }
			public bool HasInstalledSceneInstallers { get; set; }
			public bool HasScannedForSceneScope { get; set; }
		}
	}
}
