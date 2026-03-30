using System;
using System.Collections.Generic;
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
			if (firstBlock is InstanceBlock instanceBlock)
				ValidateInstanceDeclaration(infos, blocks, instanceBlock);
			else
				infos.LogError("The first block in a BindDeclaration must be Instance.", declaration);
		}

		static void ValidateInstanceDeclaration(GraphLogger infos, BindDeclarationBlockNode[] blocks, InstanceBlock instanceBlock)
		{
			instanceBlock.RefreshDynamicReferenceOptionType();

			if (blocks.Length > 2)
				infos.LogError("Instance declarations can only contain Instance plus one To block in this phase.", instanceBlock);

			if (string.IsNullOrWhiteSpace(instanceBlock.FieldName))
				infos.LogError("Instance needs a Field Name.", instanceBlock);

			Type sourceType = instanceBlock.GetValueType();
			if (sourceType == null)
				infos.LogError("Instance needs a Bind Type, unless its reference is an inferable ScriptableObject.", instanceBlock);

			UnityEngine.Object reference = instanceBlock.GetDefaultReference(sourceType);
			if (sourceType != null && reference != null)
				ValidateReferenceCompatibility(infos, instanceBlock, sourceType, reference, "Instance");

			if (blocks.Length == 1)
				return;

			if (blocks[1] is not IInjekkoDestinationBlock destinationBlock)
			{
				infos.LogError("Instance declarations must end with ToInjectableType or ToTypeInferred.", blocks[1]);
				return;
			}

			Type serviceType = destinationBlock.GetServiceType(sourceType);
			if (serviceType == null)
				infos.LogError("The selected To block could not resolve a service type.", blocks[1]);
			else if (sourceType != null && !serviceType.IsAssignableFrom(sourceType))
				infos.LogError($"Instance source type '{sourceType.FullName}' is not assignable to service type '{serviceType.FullName}'.", blocks[1]);
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
	[Node("", "Packages/com.extrys.injekko/Editor/Icons/InjekkoBindNodeIcon.png", "Bind Declaration")]
	internal sealed class BindDeclarationContextNode : ContextNode
	{
		const string k_MutedSubtitleColor = "#88888855";
		const string k_InferredTypeColor = "#4ec9b055";
		const string k_SourceTypeColor = "#4ec9b0";
		const string k_ValueColor = "#c7b3f7";
		const string k_ServiceTypeColor = "#ccd9a2";
		static readonly Color k_DefaultDeclarationColor = Color.deepSkyBlue * 0.9f;

		internal BindDeclarationBlockNode[] GetOrderedBlocks()
		{
			return BlockNodes
				.OfType<BindDeclarationBlockNode>()
				.OrderBy(static block => block.Index)
				.ToArray();
		}

		static Type GetSourceOrImplementationType(BindDeclarationBlockNode[] blocks)
			=> blocks.FirstOrDefault() is InstanceBlock instanceBlock ? instanceBlock.GetValueType() : null;

		public override void OnEnable()
		{
			//TODO: Thia is a temporary example, but in future iterations, this text should be constructed automatically based on the contained blocks to give a more accurate preview of the declaration
			//Subtitle = "<color=#88888855>Inferred bind type (</color><color=#4ec9b055>TypeA</color><color=#88888855>)</color>\nBind<<color=#4ec9b0>TypeA</color>>(<color=#c7b3f7>instanceOfTypeA</color>).To<<color=#ccd9a2>ITypeCustom</color>>().FromNew()";
			DefaultColor = k_DefaultDeclarationColor;
			ApplyPresentation(BlockNodes.OfType<BindDeclarationBlockNode>().OrderBy(static block => block.Index).ToArray());
		}

		void ApplyPresentation(BindDeclarationBlockNode[] blocks)
		{
			Subtitle = BuildSubtitle(blocks);
			DefaultColor = k_DefaultDeclarationColor;
		}

		string BuildSubtitle(BindDeclarationBlockNode[] blocks)
		{
			var lines = new List<string>(2);

			if (blocks == null || blocks.Length == 0)
			{
				lines.Add("<color=#88888855>Add an Instance block to begin.</color>");
				return string.Join("\n", lines);
			}

			if (blocks.FirstOrDefault() is InstanceBlock instanceBlock)
			{
				Type explicitType = instanceBlock.GetExplicitValueTypeOrNull();
				Type inferredType = explicitType == null ? instanceBlock.GetInferableValueTypeOrNull() : null;
				if (inferredType != null)
				{
					lines.Add(
						$"{Colorize("Inferred bind type (", k_MutedSubtitleColor)}" +
						$"{Colorize(BuildPreviewTypeName(inferredType), k_InferredTypeColor)}" +
						$"{Colorize(")", k_MutedSubtitleColor)}");
				}
			}

			lines.Add($"{BuildDeclarationPreview(blocks)}");
			return string.Join("\n", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
		} 

		string BuildDeclarationPreview(BindDeclarationBlockNode[] blocks)
		{
			Type sourceType = GetSourceOrImplementationType(blocks);
			string preview = blocks.FirstOrDefault() switch
			{
				InstanceBlock instanceBlock => BuildInstancePreview(instanceBlock, sourceType),
				_ => "Bind<?>()",
			};

			if (blocks.Length < 2 || blocks[1] is not IInjekkoDestinationBlock destinationBlock)
				return preview;

			return preview + BuildDestinationPreview(blocks[1], destinationBlock.GetServiceType(sourceType));
		}

		string BuildInstancePreview(InstanceBlock instanceBlock, Type sourceType)
		{
			string typeName = BuildPreviewTypeName(sourceType);
			string fieldName = string.IsNullOrWhiteSpace(instanceBlock.FieldName) ? "instance" : EscapeRichText(instanceBlock.FieldName);
			return $"Bind<{Colorize(typeName, k_SourceTypeColor)}>({Colorize(fieldName, k_ValueColor)})";
		}

		string BuildDestinationPreview(BindDeclarationBlockNode destinationBlock, Type serviceType)
		{
			if (destinationBlock is ToTypeInferredBlock)
				return ".ToInferredType()";

			return $".To<{Colorize(BuildPreviewTypeName(serviceType), k_ServiceTypeColor)}>()";
		}

		static string BuildPreviewTypeName(Type type)
			=> EscapeRichText(type == null ? "?" : (type.Name?.Replace("+", ".") ?? "?"));

		static string Colorize(string value, string colorHex)
			=> $"<color={colorHex}>{value}</color>";

		static string EscapeRichText(string value)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;

			return value
				.Replace("&", "&amp;")
				.Replace("<", "&lt;")
				.Replace(">", "&gt;");
		}
	}

	[Serializable]
	internal abstract class BindDeclarationBlockNode : BlockNode
	{
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
	[Node("", "", "Instance")]
	internal sealed class InstanceBlock : BindDeclarationBlockNode, IInjekkoTypeAuthoringNode
	{
		const string k_FieldNameOptionName = "FieldName";
		const string k_BindTypePortName = "Bind Type";
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
				.WithDefaultValue(fieldName);

			Type referenceOptionType = GetReferenceFieldType();
			context.AddOption(k_ReferenceOptionName, referenceOptionType)
				.WithDisplayName("Instance Reference")
				.WithDefaultValue(InjekkoNodeOptionUtility.NormalizeReferenceForType(defaultReference, referenceOptionType));
		}

		protected override void OnDefinePorts(IPortDefinitionContext context)
		{
			context.AddInputPort<MonoScript>(k_BindTypePortName).Build();
		}

		public Type GetValueType()
		{
			Type explicitType = GetExplicitValueTypeOrNull();
			if (explicitType != null)
				return explicitType;

			return GetInferableValueTypeOrNull();
		}

		public UnityEngine.Object GetDefaultReference(Type expectedType)
		{
			if (InjekkoNodeOptionUtility.TryGetOptionObjectValue(this, k_ReferenceOptionName, out UnityEngine.Object configuredReference))
				return InjekkoNodeOptionUtility.NormalizeReferenceForType(configuredReference, expectedType ?? GetInferableValueTypeOrNull());

			return InjekkoNodeOptionUtility.NormalizeReferenceForType(defaultReference, expectedType ?? GetInferableValueTypeOrNull());
		}

		internal void RefreshDynamicReferenceOptionType()
		{
			var referenceOption = GetNodeOptionByName(k_ReferenceOptionName);
			Type expectedType = GetReferenceFieldType();
			if (referenceOption != null && referenceOption.DataType == expectedType)
				return;

			defaultReference = GetDefaultReference(expectedType);
			DefineNode();
		}

		Type GetReferenceFieldType()
		{
			Type bindType = GetExplicitValueTypeOrNull();
			if (bindType == null)
				return typeof(UnityEngine.Object);

			if (typeof(UnityEngine.Object).IsAssignableFrom(bindType))
				return bindType;

			return typeof(UnityEngine.Object);
		}

		internal Type GetExplicitValueTypeOrNull()
		{
			var typePort = GetInputPortByName(k_BindTypePortName);
			var typeNode = typePort?.FirstConnectedPort?.GetNode() as IInjekkoTypeAuthoringNode;
			if (typeNode != null)
				return typeNode.GetValueType();

			if (typePort != null && typePort.TryGetValue(out MonoScript portScript))
				return portScript != null ? portScript.GetClass() : null;

			return null;
		}

		internal Type GetInferableValueTypeOrNull()
		{
			if (!InjekkoNodeOptionUtility.TryGetOptionObjectValue(this, k_ReferenceOptionName, out UnityEngine.Object configuredReference))
				configuredReference = defaultReference;

			return InjekkoNodeOptionUtility.GetInferableReferenceType(configuredReference);
		}
	}

	internal static class InjekkoNodeOptionUtility
	{
		static readonly System.Reflection.MethodInfo TryGetOptionValueMethod = typeof(INodeOption)
			.GetMethods()
			.First(static method => method.Name == nameof(INodeOption.TryGetValue) && method.IsGenericMethodDefinition);

		internal static bool TryGetOptionValue<T>(Node node, string optionName, out T value)
		{
			var option = node.GetNodeOptionByName(optionName);
			if (option != null && option.TryGetValue(out value))
				return true;

			value = default;
			return false;
		}

		internal static bool TryGetOptionObjectValue(Node node, string optionName, out UnityEngine.Object value)
		{
			value = null;
			if (node == null || string.IsNullOrWhiteSpace(optionName))
				return false;

			var option = node.GetNodeOptionByName(optionName);
			if (option == null)
				return false;

			Type dataType = option.DataType;
			if (dataType == null || !typeof(UnityEngine.Object).IsAssignableFrom(dataType))
				dataType = typeof(UnityEngine.Object);

			object[] arguments = { null };
			try
			{
				bool success = (bool)TryGetOptionValueMethod.MakeGenericMethod(dataType).Invoke(option, arguments);
				if (!success)
					return false;

				value = arguments[0] as UnityEngine.Object;
				return true;
			}
			catch
			{
				return false;
			}
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

			if (serviceType.IsInterface)
			{
				var component = ResolveInterfaceComponentCandidate(candidate, serviceType);
				if (component != null)
					return component;
			}

			return candidate;
		}

		internal static Type GetInferableReferenceType(UnityEngine.Object candidate)
		{
			return candidate is ScriptableObject scriptableObject
				? scriptableObject.GetType()
				: null;
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

		static Component ResolveInterfaceComponentCandidate(UnityEngine.Object candidate, Type interfaceType)
		{
			if (candidate is GameObject gameObject)
				return gameObject.GetComponents<Component>().FirstOrDefault(interfaceType.IsInstanceOfType);

			if (candidate is Component component)
			{
				if (interfaceType.IsInstanceOfType(component))
					return component;

				return component.gameObject != null
					? component.gameObject.GetComponents<Component>().FirstOrDefault(interfaceType.IsInstanceOfType)
					: null;
			}

			return null;
		}
	}

}
