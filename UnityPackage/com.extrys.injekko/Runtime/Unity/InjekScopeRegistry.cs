using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekScopeRegistry
	{
		static InjekCompiledScopePlan projectGraph;
		static InjekScopeNode projectScope;
		static readonly Dictionary<ulong, SceneScopeEntry> sceneScopes = new();
		static readonly Dictionary<GameObject, InjekScopeNode> anchoredScopes = new();

		public static void Configure(InjekCompiledScopePlan graphPlan)
		{
			projectGraph = graphPlan;
			projectScope = null;
			sceneScopes.Clear();
			anchoredScopes.Clear();
		}

		public static InjekScopeNode GetProjectScope()
		{
			if (projectScope != null)
				return projectScope;

			projectScope = new InjekScopeNode(projectGraph != null ? projectGraph.GraphName : "InjekkoProject", InjekScopeKind.Project, projectGraph);
			if (projectGraph != null)
				InjekGeneratedRuntimeRegistry.TryApplyGraphPlan(projectGraph, projectScope);
			return projectScope;
		}

		public static InjekScopeNode EnsureSceneScope(Scene scene)
		{
			ulong sceneKey = GetSceneKey(scene);
			return GetOrCreateSceneEntry(scene, sceneKey).Scope;
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
			return entry.Scope;
		}

		public static bool TryGetSceneScope(Scene scene, out SceneScope sceneScope)
		{
			ulong sceneKey = GetSceneKey(scene);
			if (!sceneScopes.TryGetValue(sceneKey, out var entry))
			{
				sceneScope = null;
				return false;
			}

			sceneScope = entry.SceneScopeComponent;
			return sceneScope != null;
		}

		public static InjekScopeNode EnsureGameObjectScope(GameObject gameObject)
			=> EnsureSubscope(gameObject);

		public static InjekScopeNode EnsureGameObjectScope(GameObject gameObject, IInjekScope parentScope)
		{
			if (gameObject == null)
				throw new ArgumentNullException(nameof(gameObject));
			if (parentScope == null)
				throw new ArgumentNullException(nameof(parentScope));
			if (parentScope is not InjekScopeNode parentNode)
				throw new InjekException("GameObjectScope registration requires an InjekScopeNode parent.");

			if (anchoredScopes.TryGetValue(gameObject, out var existingScope))
				return existingScope;

			InjekScopeNode scope = new(gameObject.name, InjekScopeKind.GameObject, gameObject, parentNode);
			anchoredScopes[gameObject] = scope;
			return scope;
		}

		public static InjekScopeNode EnsureSubscope(GameObject gameObject)
		{
			if (anchoredScopes.TryGetValue(gameObject, out var scope))
				return scope;

			InjekScopeNode parentScope = ResolveParentScope(gameObject);
			scope = new InjekScopeNode(gameObject.name, InjekScopeKind.GameObject, gameObject, parentScope);
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

			Transform current = gameObject.transform;
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

		public static void RegisterCachedSceneGameObjectScopes(SceneScope sceneScope)
		{
			if (sceneScope == null)
				throw new ArgumentNullException(nameof(sceneScope));
			if (sceneScope.ScopeNode == null)
				throw new InjekException("SceneScope must be registered before cached GameObject scopes can be applied.");

			foreach (var cacheEntry in sceneScope.CachedGameObjectScopes)
			{
				if (cacheEntry?.Scope == null)
					continue;

				IInjekScope parentScope = sceneScope.ScopeNode;
				if (cacheEntry.ParentScope != null && anchoredScopes.TryGetValue(cacheEntry.ParentScope.gameObject, out var parentNode))
					parentScope = parentNode;

				InjekScopeNode scopeNode = EnsureGameObjectScope(cacheEntry.Scope.gameObject, parentScope);
				cacheEntry.Scope.AssignScope(scopeNode);
				InjekGeneratedRuntimeRegistry.TryApplyGraphPlan(cacheEntry.Scope, scopeNode);
			}
		}

		static InjekScopeNode ResolveParentScope(GameObject gameObject)
		{
			Transform current = gameObject.transform.parent;
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

		static ulong GetSceneKey(Scene scene) => scene.handle.GetRawData();

		sealed class SceneScopeEntry
		{
			public SceneScopeEntry(InjekScopeNode scope)
			{
				Scope = scope;
			}

			public InjekScopeNode Scope { get; }
			public SceneScope SceneScopeComponent { get; set; }
		}
	}
}
