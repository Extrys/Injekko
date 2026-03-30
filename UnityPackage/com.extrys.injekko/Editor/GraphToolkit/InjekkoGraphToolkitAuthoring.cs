using System;
using System.Linq;
using UnityEditor;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor.GraphToolkit
{
	[Graph(AssetExtension)]
	[Serializable]
	internal sealed class InjekkoAuthoringGraph : Graph
	{
		public const string AssetExtension = "injekgraph";

		[MenuItem("Assets/Create/Injekko/Graph Toolkit/Authoring Graph", false)]
		static void CreateAssetFile()
		{
			GraphDatabase.PromptInProjectBrowserToCreateNewAsset<InjekkoAuthoringGraph>();
		}

		public override void OnGraphChanged(GraphLogger infos)
		{
			base.OnGraphChanged(infos);
			var bindingNodes = GetNodes().OfType<IInjekkoBindingAuthoringNode>().ToArray();

			foreach (var node in bindingNodes)
			{
				var contextNode = node as Node;

				if (node.RequiresReferenceSlot && string.IsNullOrWhiteSpace(node.DisplayName))
					infos.LogError("Reference-backed bindings need a variable name. Set the binding name so the scope inspector can show a clear field.", contextNode);

				if (node.GetServiceType() == null)
				{
					if (node is InjekkoBindInstanceNode)
						infos.LogError("BindInstance needs a connected Type node.", contextNode);
					else
						infos.LogError("This binding node is missing its service type.", contextNode);
				}

				if (node is InjekkoBindInstanceNode bindInstanceNode)
					ValidateBindInstance(infos, contextNode, bindInstanceNode);
			}
		}

		static void ValidateBindInstance(GraphLogger infos, Node contextNode, InjekkoBindInstanceNode bindInstanceNode)
		{
			var serviceType = bindInstanceNode.GetServiceType();
			var defaultReference = bindInstanceNode.GetDefaultReference();
			if (serviceType == null || defaultReference == null)
				return;

			var referenceType = defaultReference.GetType();
			if (serviceType.IsAssignableFrom(referenceType))
				return;

			infos.LogError(
				$"The assigned reference type '{referenceType.FullName}' is not compatible with the connected BindInstance type '{serviceType.FullName}'.",
				contextNode);
		}
	}

	internal interface IInjekkoBindingAuthoringNode
	{
		InjekGraphNodeKind Kind { get; }
		string ReferenceSlotId { get; }
		string DisplayName { get; }
		Type GetServiceType();
		Type GetImplementationType();
		bool RequiresReferenceSlot { get; }
		UnityEngine.Object GetDefaultReference();
	}

	internal interface IInjekkoTypeAuthoringNode
	{
		Type GetValueType();
	}

	[Serializable]
	internal sealed class InjekkoTypeNode : Node, IInjekkoTypeAuthoringNode
	{
		const string k_TypeOptionName = "Type";

		[SerializeField, HideInInspector] MonoScript typeScript;

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<MonoScript>(k_TypeOptionName)
				.WithDisplayName("Type")
				.WithDefaultValue(typeScript);
		}

		public Type GetValueType()
		{
			if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_TypeOptionName, out MonoScript configuredScript))
				return configuredScript != null ? configuredScript.GetClass() : null;

			return typeScript != null ? typeScript.GetClass() : null;
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<InjekkoTypePort>("Type").Build();
		}
	}

	[Serializable]
	internal sealed class InjekkoBindInstanceNode : Node, IInjekkoBindingAuthoringNode
	{
		const string k_FieldNameOptionName = "FieldName";
		const string k_ReferenceOptionName = "Reference";

		[SerializeField, HideInInspector] string bindingName = string.Empty;
		[SerializeField] string referenceSlotId = Guid.NewGuid().ToString("N");
		[SerializeField, HideInInspector] UnityEngine.Object defaultReference;

		public InjekGraphNodeKind Kind => InjekGraphNodeKind.BindInstance;
		public string ReferenceSlotId => referenceSlotId ?? string.Empty;
		public string DisplayName
		{
			get
			{
				if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_FieldNameOptionName, out string configuredName))
					return configuredName?.Trim() ?? string.Empty;

				return bindingName?.Trim() ?? string.Empty;
			}
		}
		public bool RequiresReferenceSlot => true;

		public Type GetServiceType()
		{
			var typePort = GetInputPortByName("Type");
			var typeNode = typePort?.FirstConnectedPort?.GetNode() as IInjekkoTypeAuthoringNode;
			return typeNode?.GetValueType();
		}
		public Type GetImplementationType() => null;
		public UnityEngine.Object GetDefaultReference()
		{
			if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_ReferenceOptionName, out UnityEngine.Object configuredReference))
				return InjekkoNodeOptionUtility.NormalizeReferenceForType(configuredReference, GetServiceType());

			return InjekkoNodeOptionUtility.NormalizeReferenceForType(defaultReference, GetServiceType());
		}

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<string>(k_FieldNameOptionName)
				.WithDisplayName("Field Name")
				.WithDefaultValue(bindingName)
				.Delayed();

			context.AddOption<UnityEngine.Object>(k_ReferenceOptionName)
				.WithDisplayName("Reference")
				.WithDefaultValue(defaultReference);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<InjekkoTypePort>("Type").Build();
			context.AddOutputPort<InjekkoBindingPort>("Binding").Build();
		}
	}

	[Serializable]
	internal sealed class InjekkoBindPrefabNode : Node, IInjekkoBindingAuthoringNode
	{
		[SerializeField] MonoScript componentScript;
		[SerializeField] string bindingName = string.Empty;
		[SerializeField] string referenceSlotId = Guid.NewGuid().ToString("N");
		[SerializeField] UnityEngine.Object defaultReference;

		public InjekGraphNodeKind Kind => InjekGraphNodeKind.BindPrefab;
		public string ReferenceSlotId => referenceSlotId ?? string.Empty;
		public string DisplayName => bindingName?.Trim() ?? string.Empty;
		public bool RequiresReferenceSlot => true;

		public Type GetServiceType() => componentScript != null ? componentScript.GetClass() : null;
		public Type GetImplementationType() => null;
		public UnityEngine.Object GetDefaultReference() => defaultReference;

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<InjekkoBindingPort>("Binding").Build();
		}
	}

	[Serializable]
	internal sealed class InjekkoBindTransientNode : Node, IInjekkoBindingAuthoringNode
	{
		[SerializeField] MonoScript serviceScript;
		[SerializeField] MonoScript implementationScript;

		public InjekGraphNodeKind Kind => GetImplementationType() == null
			? InjekGraphNodeKind.BindTransient
			: InjekGraphNodeKind.BindRedirectTransient;
		public string ReferenceSlotId => string.Empty;
		public string DisplayName => string.Empty;
		public bool RequiresReferenceSlot => false;

		public Type GetServiceType() => serviceScript != null ? serviceScript.GetClass() : null;
		public Type GetImplementationType() => implementationScript != null ? implementationScript.GetClass() : null;
		public UnityEngine.Object GetDefaultReference() => null;

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<InjekkoBindingPort>("Binding").Build();
		}
	}

	[Serializable]
	internal sealed class InjekkoBindScopedNode : Node, IInjekkoBindingAuthoringNode
	{
		[SerializeField] MonoScript serviceScript;
		[SerializeField] MonoScript implementationScript;

		public InjekGraphNodeKind Kind => GetImplementationType() == null
			? InjekGraphNodeKind.BindScoped
			: InjekGraphNodeKind.BindRedirectScoped;
		public string ReferenceSlotId => string.Empty;
		public string DisplayName => string.Empty;
		public bool RequiresReferenceSlot => false;

		public Type GetServiceType() => serviceScript != null ? serviceScript.GetClass() : null;
		public Type GetImplementationType() => implementationScript != null ? implementationScript.GetClass() : null;
		public UnityEngine.Object GetDefaultReference() => null;

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<InjekkoBindingPort>("Binding").Build();
		}
	}

	[Serializable]
	internal sealed class InjekkoCustomInstallerNode : Node, IInjekkoBindingAuthoringNode
	{
		[SerializeField] string bindingName = string.Empty;
		[SerializeField] string referenceSlotId = Guid.NewGuid().ToString("N");
		[SerializeField] InjekInstallerAsset defaultInstaller;

		public InjekGraphNodeKind Kind => InjekGraphNodeKind.CustomInstaller;
		public string ReferenceSlotId => referenceSlotId ?? string.Empty;
		public string DisplayName => bindingName?.Trim() ?? string.Empty;
		public bool RequiresReferenceSlot => true;

		public Type GetServiceType() => typeof(InjekInstallerAsset);
		public Type GetImplementationType() => null;
		public UnityEngine.Object GetDefaultReference() => defaultInstaller;

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddOutputPort<InjekkoBindingPort>("Binding").Build();
		}
	}

	internal sealed class InjekkoBindingPort : ScriptableObject
	{
	}

	internal sealed class InjekkoTypePort : ScriptableObject
	{
	}

	internal static class InjekkoNodeOptionUtility
	{
		internal static bool TryGetOptionValue<T>(Node node, string optionName, out T value)
		{
			var option = node.GetNodeOptionByName(optionName);
			if (option != null && option.TryGetValue(out value))
				return true;

			value = default;
			return false;
		}

		internal static UnityEngine.Object NormalizeReferenceForType(UnityEngine.Object candidate, Type serviceType)
		{
			if (candidate == null || serviceType == null)
				return candidate;

			var candidateType = candidate.GetType();
			if (serviceType.IsAssignableFrom(candidateType))
				return candidate;

			if (typeof(Component).IsAssignableFrom(serviceType))
			{
				var component = ResolveComponentCandidate(candidate, serviceType);
				if (component != null)
					return component;
			}

			return candidate;
		}

		static Component ResolveComponentCandidate(UnityEngine.Object candidate, Type componentType)
		{
			if (candidate is GameObject gameObject)
				return gameObject.GetComponent(componentType);

			if (candidate is Component component)
			{
				if (componentType.IsAssignableFrom(component.GetType()))
					return component;

				return component.gameObject != null
					? component.gameObject.GetComponent(componentType)
					: null;
			}

			return null;
		}
	}
}
