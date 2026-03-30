using System.Collections.Generic;
using UnityEngine;

namespace Injekko.Unity
{
	[DefaultExecutionOrder(-32000)]
	[DisallowMultipleComponent]
	public sealed class SceneScope : MonoBehaviour, IInjekGraphReferenceHost, IInjekScopeHost
	{
		[SerializeField] InjekCompiledScopePlan sceneGraph = null;
		[SerializeField, HideInInspector] InjekGraphReferenceBinding[] sceneBindings = null;
		[SerializeField, HideInInspector] MonoBehaviour[] cachedInjectables = null;
		[SerializeField, HideInInspector] InjekSceneScopeCacheEntry[] cachedGameObjectScopes = null;
		[SerializeField, HideInInspector] InjekSceneInjectionGraph generatedSceneInjectionGraph = null;

		bool sceneBootstrapCompleted;

		public InjekScopeNode ScopeNode { get; private set; }
		public InjekCompiledScopePlan GraphPlan => sceneGraph;
		public string GraphId => sceneGraph != null ? sceneGraph.GraphId : string.Empty;
		public bool IsBootstrapComplete => sceneBootstrapCompleted;
		internal InjekGraphReferenceBinding[] GraphBindings => sceneBindings ?? System.Array.Empty<InjekGraphReferenceBinding>();
		public IReadOnlyList<MonoBehaviour> CachedInjectables => cachedInjectables ?? System.Array.Empty<MonoBehaviour>();
		public IReadOnlyList<InjekSceneScopeCacheEntry> CachedGameObjectScopes => cachedGameObjectScopes ?? System.Array.Empty<InjekSceneScopeCacheEntry>();
		public InjekSceneInjectionGraph GeneratedSceneInjectionGraph => generatedSceneInjectionGraph ?? new InjekSceneInjectionGraph();

		void Awake()
		{
			if (sceneBootstrapCompleted)
				return;

			ScopeNode = InjekScopeRegistry.RegisterSceneScope(this);
			InjekGeneratedRuntimeRegistry.TryApplyGraphPlan(this, ScopeNode);
			InjekScopeRegistry.RegisterCachedSceneGameObjectScopes(this);
			InjekGeneratedRuntimeRegistry.TryActivateSceneScope(this);
			sceneBootstrapCompleted = true;
		}

		public UnityEngine.Object GetGraphReferenceOrNull(string slotId)
		{
			if (string.IsNullOrWhiteSpace(slotId))
				return null;

			foreach (var binding in sceneBindings ?? System.Array.Empty<InjekGraphReferenceBinding>())
			{
				if (binding != null && binding.SlotId == slotId && binding.Target != null)
					return binding.Target;
			}

			return sceneGraph != null ? sceneGraph.GetDefaultReferenceOrNull(slotId) : null;
		}

		public UnityEngine.Object GetGraphReferenceOrThrow(string slotId)
		{
			var reference = GetGraphReferenceOrNull(slotId);
			if (reference != null)
				return reference;

			throw new InjekException($"Scene graph reference '{slotId}' was not assigned on SceneScope '{name}'.");
		}

		internal void AssignScope(InjekScopeNode scopeNode)
		{
			ScopeNode = scopeNode;
		}

		internal void SetEditorGraph(InjekCompiledScopePlan graphAsset)
		{
			sceneGraph = graphAsset;
		}

		internal void SetEditorGraphBindings(InjekGraphReferenceBinding[] bindings)
		{
			sceneBindings = bindings;
		}

		internal void SetEditorCaches(MonoBehaviour[] injectables, InjekSceneScopeCacheEntry[] scopeCaches)
		{
			cachedInjectables = injectables;
			cachedGameObjectScopes = scopeCaches;
		}

		internal void SetEditorGeneratedSceneInjectionGraph(InjekSceneInjectionGraph graph)
		{
			generatedSceneInjectionGraph = graph;
		}
	}
}
