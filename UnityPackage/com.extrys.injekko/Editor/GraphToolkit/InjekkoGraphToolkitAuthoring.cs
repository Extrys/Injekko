using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

			foreach (var declaration in GetNodes().OfType<BindDeclarationContextNode>())
				ValidateDeclaration(infos, declaration);
		}

		static void ValidateDeclaration(GraphLogger infos, BindDeclarationContextNode declaration)
		{
			var blocks = declaration.GetOrderedBlocks();
			if (blocks.Length == 0)
			{
				// Empty declarations are fine while authoring; they simply don't compile to bindings yet.
				return;
			}

			BindDeclarationBlockNode firstBlock = blocks[0];
			switch (firstBlock)
			{
				case InstanceBlock instanceBlock:
					ValidateInstanceDeclaration(infos, blocks, instanceBlock);
					break;

				case TypeBlock typeBlock:
					ValidateTypeDeclaration(infos, blocks, typeBlock);
					break;

				default:
					infos.LogError("The first block in a BindDeclaration must be Instance or Type.", declaration);
					break;
			}
		}

		static void ValidateInstanceDeclaration(GraphLogger infos, BindDeclarationBlockNode[] blocks, InstanceBlock instanceBlock)
		{
			if (blocks.Length < 2)
			{
				infos.LogError("Instance declarations must be followed by a To block.", instanceBlock);
				return;
			}

			if (blocks.Length > 2)
				infos.LogError("Instance declarations can only contain Instance plus one To block in this phase.", instanceBlock);

			if (string.IsNullOrWhiteSpace(instanceBlock.FieldName))
				infos.LogError("Instance needs a Field Name.", instanceBlock);

			Type sourceType = instanceBlock.GetValueType();
			if (sourceType == null)
				infos.LogError("Instance needs a connected Bind Type input or a MonoScript value on that port.", instanceBlock);

			UnityEngine.Object reference = instanceBlock.GetDefaultReference(sourceType);
			if (reference == null)
				infos.LogError("Instance needs an Instance Reference.", instanceBlock);

			if (sourceType != null && reference != null)
				ValidateReferenceCompatibility(infos, instanceBlock, sourceType, reference, "Instance");

			if (blocks[1] is not IInjekkoDestinationBlock destinationBlock)
			{
				infos.LogError("Instance declarations must end with ToInjectableType, ToTypeInferred or ToTypeFromMonoScript.", blocks[1]);
				return;
			}

			Type serviceType = destinationBlock.GetServiceType(sourceType);
			if (serviceType == null)
				infos.LogError("The selected To block could not resolve a service type.", blocks[1]);
			else if (sourceType != null && !serviceType.IsAssignableFrom(sourceType))
				infos.LogError($"Instance source type '{sourceType.FullName}' is not assignable to service type '{serviceType.FullName}'.", blocks[1]);
		}

		static void ValidateTypeDeclaration(GraphLogger infos, BindDeclarationBlockNode[] blocks, TypeBlock typeBlock)
		{
			Type implementationType = typeBlock.GetValueType();
			if (implementationType == null)
				infos.LogError("Type needs a connected Bind Type input or a MonoScript value on that port.", typeBlock);

			if (blocks.Length < 2)
			{
				infos.LogError("Type declarations must be followed by a To block.", typeBlock);
				return;
			}

			if (blocks.Length > 2)
				infos.LogError("Type declarations can only contain Type plus one To block in this phase.", typeBlock);

			if (blocks[1] is not IInjekkoDestinationBlock destinationBlock)
			{
				infos.LogError("Type declarations must end with ToInjectableType, ToTypeInferred or ToTypeFromMonoScript.", blocks[1]);
				return;
			}

			Type serviceType = destinationBlock.GetServiceType(implementationType);
			if (serviceType == null)
				infos.LogError("The selected To block could not resolve a service type.", blocks[1]);
			else if (implementationType != null && !serviceType.IsAssignableFrom(implementationType))
				infos.LogError($"Type implementation '{implementationType.FullName}' is not assignable to service type '{serviceType.FullName}'.", blocks[1]);
		}

		static void ValidateReferenceCompatibility(GraphLogger infos, BindDeclarationBlockNode block, Type serviceType, UnityEngine.Object reference, string blockName)
		{
			Type referenceType = reference.GetType();
			if (serviceType.IsAssignableFrom(referenceType))
				return;

			infos.LogError(
				$"{blockName} reference type '{referenceType.FullName}' is not compatible with '{serviceType.FullName}'.",
				block);
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

	internal interface IInjekkoDestinationBlock
	{
		Type GetServiceType(Type sourceType);
	}

	[Serializable]
	[Node("", "", "Type")]
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
			context.AddOutputPort<MonoScript>("Type").Build();
		}
	}

	[Serializable]
	[Node("", "", "Bind Declaration")]
	internal sealed class BindDeclarationContextNode : ContextNode, IInjekkoBindingAuthoringNode
	{
		public InjekGraphNodeKind Kind
		{
			get
			{
				var blocks = GetOrderedBlocks();
				if (blocks.FirstOrDefault() is InstanceBlock)
					return InjekGraphNodeKind.BindInstance;

				Type serviceType = GetServiceType();
				Type implementationType = GetImplementationType();
				if (serviceType == null || implementationType == null)
					return InjekGraphNodeKind.BindScoped;

				return serviceType == implementationType
					? InjekGraphNodeKind.BindScoped
					: InjekGraphNodeKind.BindRedirectScoped;
			}
		}

		public string ReferenceSlotId
			=> GetSourceBlock()?.ReferenceSlotId ?? string.Empty;

		public string DisplayName
			=> GetSourceBlock()?.FieldName ?? string.Empty;

		public bool RequiresReferenceSlot => GetSourceBlock() != null;

		public Type GetServiceType()
		{
			BindDeclarationBlockNode[] blocks = GetOrderedBlocks();
			if (blocks.Length < 2 || blocks[1] is not IInjekkoDestinationBlock destinationBlock)
				return null;

			Type sourceType = GetSourceOrImplementationType(blocks);
			return destinationBlock.GetServiceType(sourceType);
		}

		public Type GetImplementationType()
		{
			BindDeclarationBlockNode[] blocks = GetOrderedBlocks();
			return blocks.FirstOrDefault() is TypeBlock typeBlock
				? typeBlock.GetValueType()
				: null;
		}

		public UnityEngine.Object GetDefaultReference()
		{
			Type sourceType = GetSourceOrImplementationType(GetOrderedBlocks());
			return GetSourceBlock()?.GetDefaultReference(sourceType);
		}

		internal BindDeclarationBlockNode[] GetOrderedBlocks()
		{
			return InjekkoContextReflectionUtility.GetContainedBlocks(this)
				.OrderBy(InjekkoContextReflectionUtility.GetOrderInContext)
				.ToArray();
		}

		IInjekkoReferenceSourceBlock GetSourceBlock()
		{
			return GetOrderedBlocks().FirstOrDefault(static block => block is IInjekkoReferenceSourceBlock) as IInjekkoReferenceSourceBlock;
		}

		static Type GetSourceOrImplementationType(BindDeclarationBlockNode[] blocks)
		{
			if (blocks.FirstOrDefault() is InstanceBlock instanceBlock)
				return instanceBlock.GetValueType();

			if (blocks.FirstOrDefault() is TypeBlock typeBlock)
				return typeBlock.GetValueType();

			return null;
		}

		public override void OnEnable()
		{
			//TODO: Thia is a temporary example, but in future iterations, this text should be constructed automatically based on the contained blocks to give a more accurate preview of the declaration
			Subtitle = "Inferred bind type (TypeA)\nBind<TypeA>(instanceOfTypeA).To<ITypeCustom>().FromNew()";
			DefaultColor = Color.deepSkyBlue * 0.9f;
		}
	}

	[Serializable]
	internal abstract class BindDeclarationBlockNode : BlockNode
	{
	}

	internal interface IInjekkoReferenceSourceBlock
	{
		string FieldName { get; }
		string ReferenceSlotId { get; }
		UnityEngine.Object GetDefaultReference(Type expectedType);
	}

	[Serializable]
	[UseWithContext(typeof(BindDeclarationContextNode))]
	[Node("", "", "Type")]
	internal sealed class TypeBlock : BindDeclarationBlockNode, IInjekkoTypeAuthoringNode
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<MonoScript>("Bind Type").Build();
		}

		public Type GetValueType()
		{
			var typePort = GetInputPortByName("Bind Type");
			var typeNode = typePort?.FirstConnectedPort?.GetNode() as IInjekkoTypeAuthoringNode;
			if (typeNode != null)
				return typeNode.GetValueType();

			if (typePort != null && typePort.TryGetValue(out MonoScript portScript))
				return portScript != null ? portScript.GetClass() : null;

			return null;
		}
	}

	[Serializable]
	[UseWithContext(typeof(BindDeclarationContextNode))]
	[Node("", "", "To Injectable Type")]
	internal sealed class ToInjectableTypeBlock : BindDeclarationBlockNode, IInjekkoDestinationBlock
	{
		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<MonoScript>("Injectable Type").Build();
		}

		public Type GetServiceType(Type sourceType)
		{
			var typePort = GetInputPortByName("Injectable Type");
			var typeNode = typePort?.FirstConnectedPort?.GetNode() as IInjekkoTypeAuthoringNode;
			if (typeNode != null)
				return typeNode.GetValueType();

			if (typePort != null && typePort.TryGetValue(out MonoScript portScript))
				return portScript != null ? portScript.GetClass() : null;

			return null;
		}
	}

	[Serializable]
	[UseWithContext(typeof(BindDeclarationContextNode))]
	[Node("", "", "To Type Inferred")]
	internal sealed class ToTypeInferredBlock : BindDeclarationBlockNode, IInjekkoDestinationBlock
	{
		public Type GetServiceType(Type sourceType) => sourceType;
	}

	[Serializable]
	[UseWithContext(typeof(BindDeclarationContextNode))]
	[Node("", "", "To Type From MonoScript")]
	internal sealed class ToTypeFromMonoScriptBlock : BindDeclarationBlockNode, IInjekkoDestinationBlock
	{
		const string k_TypeOptionName = "Type";

		[SerializeField, HideInInspector] MonoScript typeScript;

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<MonoScript>(k_TypeOptionName)
				.WithDisplayName("Type")
				.WithDefaultValue(typeScript);
		}

		public Type GetServiceType(Type sourceType)
		{
			if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_TypeOptionName, out MonoScript configuredScript))
				return configuredScript != null ? configuredScript.GetClass() : null;

			return typeScript != null ? typeScript.GetClass() : null;
		}
	}

	[Serializable]
	[UseWithContext(typeof(BindDeclarationContextNode))]
	[Node("", "", "Instance")]
	internal sealed class InstanceBlock : BindDeclarationBlockNode, IInjekkoReferenceSourceBlock, IInjekkoTypeAuthoringNode
	{
		const string k_FieldNameOptionName = "FieldName";
		const string k_ReferenceOptionName = "InstanceReference";

		[SerializeField, HideInInspector] string fieldName = string.Empty;
		[SerializeField] string referenceSlotId = Guid.NewGuid().ToString("N");
		[SerializeField, HideInInspector] UnityEngine.Object defaultReference;

		public string FieldName
		{
			get
			{
				if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_FieldNameOptionName, out string configuredName))
					return configuredName?.Trim() ?? string.Empty;

				return fieldName?.Trim() ?? string.Empty;
			}
		}

		public string ReferenceSlotId => referenceSlotId ?? string.Empty;

		protected override void OnDefineOptions(IOptionDefinitionContext context)
		{
			context.AddOption<string>(k_FieldNameOptionName)
				.WithDisplayName("Field Name")
				.WithDefaultValue(fieldName)
				.Delayed();

			context.AddOption<UnityEngine.Object>(k_ReferenceOptionName)
				.WithDisplayName("Instance Reference")
				.WithDefaultValue(defaultReference);
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<MonoScript>("Bind Type").Build();
		}

		public Type GetValueType()
		{
			var typePort = GetInputPortByName("Bind Type");
			var typeNode = typePort?.FirstConnectedPort?.GetNode() as IInjekkoTypeAuthoringNode;
			if (typeNode != null)
				return typeNode.GetValueType();

			if (typePort != null && typePort.TryGetValue(out MonoScript portScript))
				return portScript != null ? portScript.GetClass() : null;

			return null;
		}

		public UnityEngine.Object GetDefaultReference(Type expectedType)
		{
			if (InjekkoNodeOptionUtility.TryGetOptionValue(this, k_ReferenceOptionName, out UnityEngine.Object configuredReference))
				return InjekkoNodeOptionUtility.NormalizeReferenceForType(configuredReference, expectedType);

			return InjekkoNodeOptionUtility.NormalizeReferenceForType(defaultReference, expectedType);
		}
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

	internal static class InjekkoContextReflectionUtility
	{
		static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		internal static IEnumerable<BindDeclarationBlockNode> GetContainedBlocks(ContextNode context)
		{
			if (context == null)
				return Array.Empty<BindDeclarationBlockNode>();

			object contained = GetMemberValue(context, "ContainedModels")
				?? GetMemberValue(context, "Blocks")
				?? GetMemberValue(context, "ContainedNodes");

			if (contained is not IEnumerable enumerable)
				return Array.Empty<BindDeclarationBlockNode>();

			var blocks = new List<BindDeclarationBlockNode>();
			foreach (object item in enumerable)
			{
				if (item is BindDeclarationBlockNode block)
					blocks.Add(block);
			}

			return blocks;
		}

		internal static int GetOrderInContext(BindDeclarationBlockNode block)
		{
			object value = GetMemberValue(block, "OrderInContext")
				?? GetMemberValue(block, "Index");

			return value is int index ? index : 0;
		}

		static object GetMemberValue(object instance, string memberName)
		{
			if (instance == null || string.IsNullOrWhiteSpace(memberName))
				return null;

			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(memberName, Flags);
			if (property != null)
				return property.GetValue(instance);

			FieldInfo field = type.GetField(memberName, Flags);
			return field != null ? field.GetValue(instance) : null;
		}
	}
}
