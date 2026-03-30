using System;
using UnityEngine;

namespace Injekko.Unity
{
	public enum InjekGraphNodeKind
	{
		BindInstance,
		BindPrefab,
		BindTransient,
		BindScoped,
		BindRedirectTransient,
		BindRedirectScoped,
		CustomInstaller,
	}

	[Serializable]
	public struct InjekGraphTypeReference
	{
		[SerializeField] string assemblyQualifiedName;
		[SerializeField] string qualifiedTypeName;

		public string AssemblyQualifiedName => assemblyQualifiedName ?? string.Empty;
		public string QualifiedTypeName => qualifiedTypeName ?? string.Empty;

		public bool IsAssigned => !string.IsNullOrWhiteSpace(qualifiedTypeName);

		public void Assign(Type type)
		{
			assemblyQualifiedName = type?.AssemblyQualifiedName ?? string.Empty;
			qualifiedTypeName = type?.FullName ?? string.Empty;
		}

		internal void SetRaw(string assemblyQualifiedName, string qualifiedTypeName)
		{
			this.assemblyQualifiedName = assemblyQualifiedName ?? string.Empty;
			this.qualifiedTypeName = qualifiedTypeName ?? string.Empty;
		}
	}

	[Serializable]
	public sealed class InjekGraphReferenceBinding
	{
		[SerializeField] string slotId = string.Empty;
		[SerializeField] UnityEngine.Object target;

		public InjekGraphReferenceBinding()
		{
		}

		internal InjekGraphReferenceBinding(string slotId, UnityEngine.Object target)
		{
			this.slotId = slotId ?? string.Empty;
			this.target = target;
		}

		public string SlotId => slotId ?? string.Empty;
		public UnityEngine.Object Target => target;
	}

	[Serializable]
	public sealed class InjekSceneScopeCacheEntry
	{
		[SerializeField] GameObjectScope scope;
		[SerializeField] GameObjectScope parentScope;

		public InjekSceneScopeCacheEntry()
		{
		}

		internal InjekSceneScopeCacheEntry(GameObjectScope scope, GameObjectScope parentScope)
		{
			this.scope = scope;
			this.parentScope = parentScope;
		}

		public GameObjectScope Scope => scope;
		public GameObjectScope ParentScope => parentScope;
	}

	public interface IInjekGraphReferenceHost
	{
		InjekCompiledScopePlan GraphPlan { get; }
		string GraphId { get; }
		UnityEngine.Object GetGraphReferenceOrNull(string slotId);
		UnityEngine.Object GetGraphReferenceOrThrow(string slotId);
	}

	[Serializable]
	public sealed class InjekCompiledBindingDefinition
	{
		[SerializeField] InjekGraphNodeKind kind;
		[SerializeField] string displayName = string.Empty;
		[SerializeField] string referenceSlotId = string.Empty;
		[SerializeField] bool requiresReferenceSlot;
		[SerializeField] InjekGraphTypeReference serviceType;
		[SerializeField] InjekGraphTypeReference implementationType;
		[SerializeField] UnityEngine.Object defaultReference;

		public InjekCompiledBindingDefinition()
		{
		}

		internal InjekCompiledBindingDefinition(
			InjekGraphNodeKind kind,
			string displayName,
			string referenceSlotId,
			bool requiresReferenceSlot,
			InjekGraphTypeReference serviceType,
			InjekGraphTypeReference implementationType,
			UnityEngine.Object defaultReference)
		{
			this.kind = kind;
			this.displayName = displayName ?? string.Empty;
			this.referenceSlotId = referenceSlotId ?? string.Empty;
			this.requiresReferenceSlot = requiresReferenceSlot;
			this.serviceType = serviceType;
			this.implementationType = implementationType;
			this.defaultReference = defaultReference;
		}

		public InjekGraphNodeKind Kind => kind;
		public string DisplayName => displayName ?? string.Empty;
		public string ReferenceSlotId => referenceSlotId ?? string.Empty;
		public bool RequiresReferenceSlot => requiresReferenceSlot;
		public InjekGraphTypeReference ServiceType => serviceType;
		public InjekGraphTypeReference ImplementationType => implementationType;
		public UnityEngine.Object DefaultReference => defaultReference;
	}

	public interface IInjekScopeHost
	{
		InjekScopeNode ScopeNode { get; }
	}

	public enum InjekSceneInjectionNodeKind
	{
		SceneScope,
		GameObjectScope,
		Injectable,
	}

	[Serializable]
	public sealed class InjekSceneInjectionNode
	{
		[SerializeField] string nodeId = string.Empty;
		[SerializeField] string parentNodeId = string.Empty;
		[SerializeField] InjekSceneInjectionNodeKind kind;
		[SerializeField] string displayName = string.Empty;
		[SerializeField] string hierarchyPath = string.Empty;
		[SerializeField] GameObject targetGameObject;
		[SerializeField] Component targetComponent;

		public InjekSceneInjectionNode()
		{
		}

		internal InjekSceneInjectionNode(
			string nodeId,
			string parentNodeId,
			InjekSceneInjectionNodeKind kind,
			string displayName,
			string hierarchyPath,
			GameObject targetGameObject,
			Component targetComponent)
		{
			this.nodeId = nodeId ?? string.Empty;
			this.parentNodeId = parentNodeId ?? string.Empty;
			this.kind = kind;
			this.displayName = displayName ?? string.Empty;
			this.hierarchyPath = hierarchyPath ?? string.Empty;
			this.targetGameObject = targetGameObject;
			this.targetComponent = targetComponent;
		}

		public string NodeId => nodeId ?? string.Empty;
		public string ParentNodeId => parentNodeId ?? string.Empty;
		public InjekSceneInjectionNodeKind Kind => kind;
		public string DisplayName => displayName ?? string.Empty;
		public string HierarchyPath => hierarchyPath ?? string.Empty;
		public GameObject TargetGameObject => targetGameObject;
		public Component TargetComponent => targetComponent;
	}

	[Serializable]
	public sealed class InjekSceneInjectionGraph
	{
		[SerializeField] string scenePath = string.Empty;
		[SerializeField] InjekSceneInjectionNode[] nodes = null;

		public InjekSceneInjectionGraph()
		{
		}

		internal InjekSceneInjectionGraph(string scenePath, InjekSceneInjectionNode[] nodes)
		{
			this.scenePath = scenePath ?? string.Empty;
			this.nodes = nodes;
		}

		public string ScenePath => scenePath ?? string.Empty;
		public InjekSceneInjectionNode[] Nodes => nodes ?? Array.Empty<InjekSceneInjectionNode>();
	}
}
