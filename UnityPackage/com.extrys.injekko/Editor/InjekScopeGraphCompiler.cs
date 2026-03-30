using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Injekko;
using Injekko.Unity;
using Injekko.Editor.GraphToolkit;

namespace Injekko.Editor
{
	internal static class InjekScopeGraphCompiler
	{
		internal const string GeneratedSourcePath = InjekGraphPlanCodeGenerator.GeneratedSourcePath;

		public static void CompileAll(bool refreshAssets = true)
		{
			var projectAssets = FindProjectAssets();
			var sceneScopes = FindLoadedSceneScopes();
			var gameObjectScopes = FindLoadedGameObjectScopes();

			foreach (var projectAsset in projectAssets)
				RefreshProjectAssetBindings(projectAsset);

			foreach (var sceneScope in sceneScopes)
				RefreshSceneScopeCache(sceneScope);

			foreach (var gameObjectScope in gameObjectScopes)
				RefreshGameObjectScopeBindings(gameObjectScope);

			InjekGraphPlanCodeGenerator.GenerateAndWriteAll(refreshAssets);
		}

		public static void RefreshSceneScopeCache(SceneScope sceneScope)
		{
			if (sceneScope == null)
				return;

			var bindings = BuildReferenceBindings(sceneScope.GraphPlan, sceneScope);
			bool bindingsChanged = !AreBindingsEqual(sceneScope.GraphBindings, bindings);
			if (bindingsChanged)
				sceneScope.SetEditorGraphBindings(bindings);

			var injectables = BuildInjectableCache(sceneScope).ToArray();
			var scopeCaches = BuildSceneScopeCache(sceneScope).ToArray();
			var generatedGraph = BuildGeneratedSceneInjectionGraph(sceneScope);
			bool cachesChanged = !AreInjectablesEqual(sceneScope.CachedInjectables, injectables)
				|| !AreSceneScopeCachesEqual(sceneScope.CachedGameObjectScopes, scopeCaches)
				|| !AreGeneratedGraphsEqual(sceneScope.GeneratedSceneInjectionGraph, generatedGraph);

			if (cachesChanged)
			{
				sceneScope.SetEditorCaches(injectables, scopeCaches);
				sceneScope.SetEditorGeneratedSceneInjectionGraph(generatedGraph);
			}

			if (!bindingsChanged && !cachesChanged)
				return;

			EditorUtility.SetDirty(sceneScope);
			if (sceneScope.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
		}

		public static void RefreshProjectAssetBindings(InjekkoProjectAsset projectAsset)
		{
			if (projectAsset == null)
				return;

			var bindings = BuildReferenceBindings(projectAsset.GraphPlan, projectAsset);
			if (AreBindingsEqual(projectAsset.GraphBindings, bindings))
				return;

			projectAsset.SetEditorGraphBindings(bindings);
			EditorUtility.SetDirty(projectAsset);
		}

		public static void RefreshGameObjectScopeBindings(GameObjectScope gameObjectScope)
		{
			if (gameObjectScope == null)
				return;

			var bindings = BuildReferenceBindings(gameObjectScope.GraphPlan, gameObjectScope);
			if (AreBindingsEqual(gameObjectScope.GraphBindings, bindings))
				return;

			gameObjectScope.SetEditorGraphBindings(bindings);
			EditorUtility.SetDirty(gameObjectScope);
			if (gameObjectScope.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(gameObjectScope.gameObject.scene);
		}

		static IReadOnlyList<InjekkoProjectAsset> FindProjectAssets()
		{
			return AssetDatabase.FindAssets("t:InjekkoProjectAsset")
				.Select(AssetDatabase.GUIDToAssetPath)
				.OrderBy(static path => path, StringComparer.Ordinal)
				.Select(static path => AssetDatabase.LoadAssetAtPath<InjekkoProjectAsset>(path))
				.Where(static asset => asset != null)
				.ToArray();
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

		static InjekGraphReferenceBinding[] BuildReferenceBindings(InjekCompiledScopePlan graphPlan, IInjekGraphReferenceHost host)
		{
			if (graphPlan == null)
				return Array.Empty<InjekGraphReferenceBinding>();

			var existingBySlot = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
			foreach (var node in GetExistingBindings(host))
			{
				if (node == null || string.IsNullOrWhiteSpace(node.SlotId) || existingBySlot.ContainsKey(node.SlotId))
					continue;

				existingBySlot.Add(node.SlotId, node.Target);
			}

			var bindings = new List<InjekGraphReferenceBinding>();
			foreach (var node in graphPlan.BindingDefinitions.Select(InjekkoBindingAuthoringDefinition.FromCompiledDefinition))
			{
				if (!node.RequiresReferenceSlot || string.IsNullOrWhiteSpace(node.ReferenceSlotId))
					continue;

				existingBySlot.TryGetValue(node.ReferenceSlotId, out var target);
				if (target != null)
					bindings.Add(CreateReferenceBinding(node.ReferenceSlotId, target));
			}

			return bindings.ToArray();
		}

		static IEnumerable<InjekGraphReferenceBinding> GetExistingBindings(IInjekGraphReferenceHost host)
		{
			if (host is InjekkoProjectAsset projectAsset)
				return projectAsset.GraphBindings ?? Array.Empty<InjekGraphReferenceBinding>();

			if (host is SceneScope sceneScope)
				return sceneScope.GraphBindings ?? Array.Empty<InjekGraphReferenceBinding>();

			if (host is GameObjectScope gameObjectScope)
				return gameObjectScope.GraphBindings ?? Array.Empty<InjekGraphReferenceBinding>();

			return Array.Empty<InjekGraphReferenceBinding>();
		}

		static InjekGraphReferenceBinding CreateReferenceBinding(string slotId, UnityEngine.Object target)
			=> new(slotId, target);

		static bool AreBindingsEqual(IReadOnlyList<InjekGraphReferenceBinding> current, IReadOnlyList<InjekGraphReferenceBinding> next)
		{
			current ??= Array.Empty<InjekGraphReferenceBinding>();
			next ??= Array.Empty<InjekGraphReferenceBinding>();

			if (current.Count != next.Count)
				return false;

			for (int index = 0; index < current.Count; index++)
			{
				var currentBinding = current[index];
				var nextBinding = next[index];

				string currentSlotId = currentBinding?.SlotId ?? string.Empty;
				string nextSlotId = nextBinding?.SlotId ?? string.Empty;
				if (!string.Equals(currentSlotId, nextSlotId, StringComparison.Ordinal))
					return false;

				if (!ReferenceEquals(currentBinding?.Target, nextBinding?.Target))
					return false;
			}

			return true;
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
				if (!ReferenceEquals(currentEntry?.Scope, nextEntry?.Scope)
					|| !ReferenceEquals(currentEntry?.ParentScope, nextEntry?.ParentScope))
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

		static IEnumerable<MonoBehaviour> BuildInjectableCache(SceneScope sceneScope)
		{
			var injectableTypes = GetInjectableComponentTypes();
			var components = sceneScope.gameObject.scene.GetRootGameObjects()
				.SelectMany(static root => root.GetComponentsInChildren<MonoBehaviour>(true))
				.Where(component => component != null && injectableTypes.Contains(component.GetType()))
				.OrderBy(component => GetTransformDepth(component.transform))
				.ThenBy(component => GetHierarchyPath(component.transform), StringComparer.Ordinal);

			return components;
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

				yield return CreateSceneScopeCacheEntry(scope, parentScope);
			}
		}

		static InjekSceneScopeCacheEntry CreateSceneScopeCacheEntry(GameObjectScope scope, GameObjectScope parentScope)
			=> new(scope, parentScope);

		static InjekSceneInjectionGraph BuildGeneratedSceneInjectionGraph(SceneScope sceneScope)
		{
			const string sceneRootNodeId = "scene_scope_root";
			var nodes = new List<InjekSceneInjectionNode>
			{
				new(
					sceneRootNodeId,
					string.Empty,
					InjekSceneInjectionNodeKind.SceneScope,
					$"SceneScope ({sceneScope.name})",
					sceneScope.gameObject.scene.path,
					sceneScope.gameObject,
					sceneScope)
			};

			var scopeNodeIds = new Dictionary<GameObjectScope, string>();
			foreach (var entry in sceneScope.CachedGameObjectScopes)
			{
				if (entry?.Scope == null)
					continue;

				string nodeId = $"goscope_{entry.Scope.GetEntityId()}";
				scopeNodeIds[entry.Scope] = nodeId;
				string parentId = entry.ParentScope != null && scopeNodeIds.TryGetValue(entry.ParentScope, out var cachedParentId)
					? cachedParentId
					: sceneRootNodeId;
				nodes.Add(new InjekSceneInjectionNode(
					nodeId,
					parentId,
					InjekSceneInjectionNodeKind.GameObjectScope,
					$"GameObjectScope ({entry.Scope.name})",
					GetHierarchyPath(entry.Scope.transform),
					entry.Scope.gameObject,
					entry.Scope));
			}

			foreach (var injectable in sceneScope.CachedInjectables)
			{
				if (injectable == null)
					continue;

				string parentId = sceneRootNodeId;
				var current = injectable.transform;
				while (current != null)
				{
					var parentScope = current.GetComponent<GameObjectScope>();
					if (parentScope != null && scopeNodeIds.TryGetValue(parentScope, out var scopeNodeId))
					{
						parentId = scopeNodeId;
						break;
					}

					current = current.parent;
				}

				nodes.Add(new InjekSceneInjectionNode(
					$"injectable_{injectable.GetEntityId()}",
					parentId,
					InjekSceneInjectionNodeKind.Injectable,
					$"{injectable.GetType().Name} ({injectable.name})",
					GetHierarchyPath(injectable.transform),
					injectable.gameObject,
					injectable));
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

					var injectMethod = type
						.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
						.FirstOrDefault(static method => method.GetCustomAttribute<InjekAttribute>() != null);
					if (injectMethod != null)
						result.Add(type);
				}
			}

			return result;
		}

		static InjekGraphMetadata TryCreateGeneratedMetadata()
		{
			Type metadataFactoryType = AppDomain.CurrentDomain
				.GetAssemblies()
				.Select(static assembly => assembly.GetType("Injekko_GraphMetadata", throwOnError: false))
				.FirstOrDefault(static type => type != null);
			if (metadataFactoryType == null)
				return null;

			MethodInfo createMethod = metadataFactoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
			if (createMethod == null)
				return null;

			return createMethod.Invoke(null, null) as InjekGraphMetadata;
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

		static int GetTransformDepth(Transform transform)
		{
			int depth = 0;
			var current = transform;
			while (current != null)
			{
				depth++;
				current = current.parent;
			}

			return depth;
		}

		static string GetHierarchyPath(Transform transform)
		{
			var path = transform.name;
			var current = transform.parent;
			while (current != null)
			{
				path = current.name + "/" + path;
				current = current.parent;
			}

			return path;
		}
	}
}
