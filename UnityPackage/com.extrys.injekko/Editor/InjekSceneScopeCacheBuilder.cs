using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor
{
	internal static class InjekSceneScopeCacheBuilder
	{
		internal static void RefreshSceneScopeCaches(SceneScope sceneScope)
		{
			if (sceneScope == null)
				return;

			var injectables = BuildInjectableCache(sceneScope).ToArray();
			var scopeCaches = BuildSceneScopeCache(sceneScope).ToArray();
			var generatedGraph = BuildGeneratedSceneInjectionGraph(sceneScope, injectables, scopeCaches);
			bool changed = !AreInjectablesEqual(sceneScope.CachedInjectables, injectables)
				|| !AreSceneScopeCachesEqual(sceneScope.CachedGameObjectScopes, scopeCaches)
				|| !AreGeneratedGraphsEqual(sceneScope.GeneratedSceneInjectionGraph, generatedGraph);
			if (!changed)
				return;

			sceneScope.SetEditorCaches(injectables, scopeCaches);
			sceneScope.SetEditorGeneratedSceneInjectionGraph(generatedGraph);
			EditorUtility.SetDirty(sceneScope);
			if (sceneScope.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
		}

		static IEnumerable<MonoBehaviour> BuildInjectableCache(SceneScope sceneScope)
		{
			var injectableTypes = GetInjectableComponentTypes();
			return sceneScope.gameObject.scene.GetRootGameObjects()
				.SelectMany(static root => root.GetComponentsInChildren<MonoBehaviour>(true))
				.Where(component => component != null && injectableTypes.Contains(component.GetType()))
				.OrderBy(component => GetTransformDepth(component.transform))
				.ThenBy(component => GetHierarchyPath(component.transform), StringComparer.Ordinal);
		}

		static IEnumerable<InjekSceneScopeCacheEntry> BuildSceneScopeCache(SceneScope sceneScope)
		{
			var scopes = sceneScope.gameObject.scene.GetRootGameObjects()
				.SelectMany(static root => root.GetComponentsInChildren<GameObjectScope>(true))
				.Where(static scope => scope != null)
				.OrderBy(scope => GetTransformDepth(scope.transform))
				.ThenBy(scope => GetHierarchyPath(scope.transform), StringComparer.Ordinal)
				.ToArray();

			foreach (var scope in scopes)
			{
				GameObjectScope parentScope = null;
				var current = scope.transform.parent;
				while (current != null)
				{
					parentScope = current.GetComponent<GameObjectScope>();
					if (parentScope != null)
						break;
					current = current.parent;
				}

				yield return new InjekSceneScopeCacheEntry(scope, parentScope);
			}
		}

		static InjekSceneInjectionGraph BuildGeneratedSceneInjectionGraph(SceneScope sceneScope, IReadOnlyList<MonoBehaviour> injectables, IReadOnlyList<InjekSceneScopeCacheEntry> scopeCaches)
		{
			const string sceneRootNodeId = "scene_scope_root";
			var nodes = new List<InjekSceneInjectionNode>
			{
				new(sceneRootNodeId, string.Empty, InjekSceneInjectionNodeKind.SceneScope, $"SceneScope ({sceneScope.name})", sceneScope.gameObject.scene.path, sceneScope.gameObject, sceneScope)
			};
			var scopeNodeIds = new Dictionary<GameObjectScope, string>();

			foreach (var entry in scopeCaches ?? Array.Empty<InjekSceneScopeCacheEntry>())
			{
				if (entry?.Scope == null)
					continue;

				string nodeId = $"goscope_{entry.Scope.GetEntityId()}";
				scopeNodeIds[entry.Scope] = nodeId;
				string parentId = entry.ParentScope != null && scopeNodeIds.TryGetValue(entry.ParentScope, out var cachedParentId) ? cachedParentId : sceneRootNodeId;
				nodes.Add(new InjekSceneInjectionNode(nodeId, parentId, InjekSceneInjectionNodeKind.GameObjectScope, $"GameObjectScope ({entry.Scope.name})", GetHierarchyPath(entry.Scope.transform), entry.Scope.gameObject, entry.Scope));
			}

			foreach (var injectable in injectables ?? Array.Empty<MonoBehaviour>())
			{
				if (injectable == null)
					continue;

				string parentId = sceneRootNodeId;
				for (var current = injectable.transform; current != null; current = current.parent)
				{
					var parentScope = current.GetComponent<GameObjectScope>();
					if (parentScope == null || !scopeNodeIds.TryGetValue(parentScope, out var scopeNodeId))
						continue;

					parentId = scopeNodeId;
					break;
				}

				nodes.Add(new InjekSceneInjectionNode($"injectable_{injectable.GetEntityId()}", parentId, InjekSceneInjectionNodeKind.Injectable, $"{injectable.GetType().Name} ({injectable.name})", GetHierarchyPath(injectable.transform), injectable.gameObject, injectable));
			}

			return new InjekSceneInjectionGraph(sceneScope.gameObject.scene.path, nodes.ToArray());
		}

		static HashSet<Type> GetInjectableComponentTypes()
		{
			var generatedMetadata = TryCreateGeneratedMetadata();
			if (generatedMetadata != null)
			{
				return new HashSet<Type>(generatedMetadata.Types
					.Where(static info => info.HasInjekMethod)
					.Select(static info => ResolveType(info.TypeName))
					.Where(static type => type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
					.Cast<Type>());
			}

			var result = new HashSet<Type>();
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.IsDynamic)
					continue;

				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException exception)
				{
					types = exception.Types.Where(static type => type != null).ToArray();
				}

				foreach (var type in types)
				{
					if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
						continue;

					var injectMethod = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						.FirstOrDefault(static method => method.GetCustomAttribute<InjekAttribute>() != null);
					if (injectMethod != null)
						result.Add(type);
				}
			}

			return result;
		}

		static InjekGraphMetadata TryCreateGeneratedMetadata()
		{
			Type metadataFactoryType = AppDomain.CurrentDomain.GetAssemblies()
				.Select(static assembly => assembly.GetType("Injekko_GraphMetadata", throwOnError: false))
				.FirstOrDefault(static type => type != null);
			if (metadataFactoryType == null)
				return null;

			MethodInfo createMethod = metadataFactoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
			return createMethod?.Invoke(null, null) as InjekGraphMetadata;
		}

		static Type ResolveType(string typeName)
		{
			if (string.IsNullOrWhiteSpace(typeName))
				return null;
			if (typeName.StartsWith("global::", StringComparison.Ordinal))
				typeName = typeName.Substring("global::".Length);

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var resolvedType = assembly.GetType(typeName, throwOnError: false);
				if (resolvedType != null)
					return resolvedType;
			}

			return Type.GetType(typeName, throwOnError: false);
		}

		static bool AreInjectablesEqual(IReadOnlyList<MonoBehaviour> current, IReadOnlyList<MonoBehaviour> next)
		{
			current ??= Array.Empty<MonoBehaviour>();
			next ??= Array.Empty<MonoBehaviour>();
			if (current.Count != next.Count)
				return false;

			for (int index = 0; index < current.Count; index++)
			{
				if (!ReferenceEquals(current[index], next[index]))
					return false;
			}

			return true;
		}

		static bool AreSceneScopeCachesEqual(IReadOnlyList<InjekSceneScopeCacheEntry> current, IReadOnlyList<InjekSceneScopeCacheEntry> next)
		{
			current ??= Array.Empty<InjekSceneScopeCacheEntry>();
			next ??= Array.Empty<InjekSceneScopeCacheEntry>();
			if (current.Count != next.Count)
				return false;

			for (int index = 0; index < current.Count; index++)
			{
				var currentEntry = current[index];
				var nextEntry = next[index];
				if (!ReferenceEquals(currentEntry?.Scope, nextEntry?.Scope) || !ReferenceEquals(currentEntry?.ParentScope, nextEntry?.ParentScope))
					return false;
			}

			return true;
		}

		static bool AreGeneratedGraphsEqual(InjekSceneInjectionGraph current, InjekSceneInjectionGraph next)
		{
			if (ReferenceEquals(current, next))
				return true;
			if (current == null || next == null)
				return false;
			if (!string.Equals(current.ScenePath, next.ScenePath, StringComparison.Ordinal))
				return false;

			var currentNodes = current.Nodes ?? Array.Empty<InjekSceneInjectionNode>();
			var nextNodes = next.Nodes ?? Array.Empty<InjekSceneInjectionNode>();
			if (currentNodes.Length != nextNodes.Length)
				return false;

			for (int index = 0; index < currentNodes.Length; index++)
			{
				var currentNode = currentNodes[index];
				var nextNode = nextNodes[index];
				if (currentNode == null || nextNode == null)
				{
					if (!ReferenceEquals(currentNode, nextNode))
						return false;
					continue;
				}

				if (!string.Equals(currentNode.NodeId, nextNode.NodeId, StringComparison.Ordinal)
					|| !string.Equals(currentNode.ParentNodeId, nextNode.ParentNodeId, StringComparison.Ordinal)
					|| currentNode.Kind != nextNode.Kind
					|| !string.Equals(currentNode.DisplayName, nextNode.DisplayName, StringComparison.Ordinal)
					|| !string.Equals(currentNode.HierarchyPath, nextNode.HierarchyPath, StringComparison.Ordinal)
					|| !ReferenceEquals(currentNode.TargetGameObject, nextNode.TargetGameObject)
					|| !ReferenceEquals(currentNode.TargetComponent, nextNode.TargetComponent))
					return false;
			}

			return true;
		}

		static int GetTransformDepth(Transform transform)
		{
			int depth = 0;
			for (var current = transform; current != null; current = current.parent)
				depth++;
			return depth;
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
