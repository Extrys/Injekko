using UnityEngine;

namespace Injekko.Unity
{
	[DisallowMultipleComponent]
	public sealed class GameObjectScope : MonoBehaviour, IInjekGraphReferenceHost, IInjekScopeHost
	{
		[SerializeField] InjekCompiledScopePlan graph = null;
		[SerializeField, HideInInspector] InjekGraphReferenceBinding[] graphBindings = null;

		public InjekScopeNode ScopeNode { get; private set; }
		public InjekCompiledScopePlan GraphPlan => graph;
		public string GraphId => graph != null ? graph.GraphId : string.Empty;
		internal InjekGraphReferenceBinding[] GraphBindings => graphBindings ?? System.Array.Empty<InjekGraphReferenceBinding>();

		void Awake()
		{
			ScopeNode = InjekScopeRegistry.EnsureGameObjectScope(gameObject);
			InjekGeneratedRuntimeRegistry.TryApplyGraphPlan(this, ScopeNode);
		}

		public UnityEngine.Object GetGraphReferenceOrNull(string slotId)
		{
			if (string.IsNullOrWhiteSpace(slotId))
				return null;

			foreach (var binding in graphBindings ?? System.Array.Empty<InjekGraphReferenceBinding>())
			{
				if (binding != null && binding.SlotId == slotId && binding.Target != null)
					return binding.Target;
			}

			return graph != null ? graph.GetDefaultReferenceOrNull(slotId) : null;
		}

		public UnityEngine.Object GetGraphReferenceOrThrow(string slotId)
		{
			var reference = GetGraphReferenceOrNull(slotId);
			if (reference != null)
				return reference;

			throw new InjekException($"Graph reference '{slotId}' was not assigned on GameObjectScope '{name}'.");
		}

		internal void AssignScope(InjekScopeNode scopeNode)
		{
			ScopeNode = scopeNode;
		}

		internal void SetEditorGraph(InjekCompiledScopePlan graphAsset)
		{
			graph = graphAsset;
		}

		internal void SetEditorGraphBindings(InjekGraphReferenceBinding[] bindings)
		{
			graphBindings = bindings;
		}
	}
}
