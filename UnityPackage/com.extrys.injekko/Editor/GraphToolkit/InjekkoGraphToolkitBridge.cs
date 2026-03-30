using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor.GraphToolkit
{
	internal readonly struct InjekkoBindingAuthoringDefinition
	{
		public InjekkoBindingAuthoringDefinition(
			InjekGraphNodeKind kind,
			string displayName,
			string referenceSlotId,
			Type serviceType,
			Type implementationType,
			bool requiresReferenceSlot,
			UnityEngine.Object defaultReference)
		{
			Kind = kind;
			DisplayName = displayName ?? string.Empty;
			ReferenceSlotId = referenceSlotId ?? string.Empty;
			ServiceType = serviceType;
			ImplementationType = implementationType;
			RequiresReferenceSlot = requiresReferenceSlot;
			DefaultReference = defaultReference;
		}

		public InjekGraphNodeKind Kind { get; }
		public string DisplayName { get; }
		public string ReferenceSlotId { get; }
		public Type ServiceType { get; }
		public Type ImplementationType { get; }
		public bool RequiresReferenceSlot { get; }
		public UnityEngine.Object DefaultReference { get; }

		public Type GetExpectedReferenceType()
		{
			if (Kind == InjekGraphNodeKind.CustomInstaller)
				return typeof(InjekInstallerAsset);

			if (!RequiresReferenceSlot)
				return typeof(UnityEngine.Object);

			if (ServiceType != null && typeof(UnityEngine.Object).IsAssignableFrom(ServiceType))
				return ServiceType;

			return typeof(UnityEngine.Object);
		}

		public InjekCompiledBindingDefinition ToCompiledDefinition()
		{
			var serviceReference = new InjekGraphTypeReference();
			if (ServiceType != null)
				serviceReference.Assign(ServiceType);

			var implementationReference = new InjekGraphTypeReference();
			if (ImplementationType != null)
				implementationReference.Assign(ImplementationType);

			return new InjekCompiledBindingDefinition(
				Kind,
				DisplayName,
				ReferenceSlotId,
				RequiresReferenceSlot,
				serviceReference,
				implementationReference,
				DefaultReference);
		}

		public static InjekkoBindingAuthoringDefinition FromCompiledDefinition(InjekCompiledBindingDefinition definition)
		{
			return new InjekkoBindingAuthoringDefinition(
				definition.Kind,
				definition.DisplayName,
				definition.ReferenceSlotId,
				ResolveType(definition.ServiceType),
				ResolveType(definition.ImplementationType),
				definition.RequiresReferenceSlot,
				definition.DefaultReference);
		}

		static Type ResolveType(InjekGraphTypeReference typeReference)
		{
			if (!typeReference.IsAssigned)
				return null;

			if (!string.IsNullOrWhiteSpace(typeReference.AssemblyQualifiedName))
			{
				var assemblyQualifiedType = Type.GetType(typeReference.AssemblyQualifiedName, throwOnError: false);
				if (assemblyQualifiedType != null)
					return assemblyQualifiedType;
			}

			if (string.IsNullOrWhiteSpace(typeReference.QualifiedTypeName))
				return null;

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var resolvedType = assembly.GetType(typeReference.QualifiedTypeName, throwOnError: false);
				if (resolvedType != null)
					return resolvedType;
			}

			return Type.GetType(typeReference.QualifiedTypeName, throwOnError: false);
		}
	}

	internal static class InjekkoGraphToolkitBridge
	{
		public static string GetAuthoringGraphAssetPath(InjekCompiledScopePlan graphPlan)
			=> graphPlan == null ? string.Empty : AssetDatabase.GetAssetPath(graphPlan);

		public static bool HasAuthoringGraph(InjekCompiledScopePlan graphPlan)
			=> !string.IsNullOrWhiteSpace(GetAuthoringGraphAssetPath(graphPlan));

		public static void OpenAuthoringGraph(InjekCompiledScopePlan graphPlan)
		{
			if (graphPlan == null)
				return;

			AssetDatabase.OpenAsset(graphPlan);
		}

		public static IReadOnlyList<IInjekkoBindingAuthoringNode> GetBindingNodes(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return Array.Empty<IInjekkoBindingAuthoringNode>();

			var graph = GraphDatabase.LoadGraphForImporter<InjekkoAuthoringGraph>(assetPath);
			if (graph == null)
				return Array.Empty<IInjekkoBindingAuthoringNode>();

			var directNodes = graph.GetNodes()
				.OfType<IInjekkoBindingAuthoringNode>()
				.ToArray();
			if (directNodes.Length > 0)
				return directNodes;

			return InjekkoGraphReflectionUtility.GetBindingNodes(graph);
		}

		public static IReadOnlyList<IInjekkoBindingAuthoringNode> GetBindingNodes(InjekCompiledScopePlan graphPlan)
			=> GetBindingNodes(GetAuthoringGraphAssetPath(graphPlan));

		public static IReadOnlyList<InjekkoBindingAuthoringDefinition> GetBindingDefinitions(InjekCompiledScopePlan graphPlan)
		{
			if (graphPlan == null)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			if (graphPlan.BindingDefinitions.Length > 0)
				return graphPlan.BindingDefinitions.Select(InjekkoBindingAuthoringDefinition.FromCompiledDefinition).ToArray();

			return Array.Empty<InjekkoBindingAuthoringDefinition>();
		}

		public static IReadOnlyList<InjekkoBindingAuthoringDefinition> GetBindingDefinitions(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var graph = GraphDatabase.LoadGraphForImporter<InjekkoAuthoringGraph>(assetPath);
			if (graph == null)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var contextualDefinitions = InjekkoGraphReflectionUtility.GetBindingDefinitions(graph);
			if (contextualDefinitions.Count > 0)
				return contextualDefinitions;

			return GetBindingNodes(assetPath).Select(CreateDefinition).ToArray();
		}

		static InjekkoBindingAuthoringDefinition CreateDefinition(IInjekkoBindingAuthoringNode node)
		{
			Type serviceType = node.GetServiceType();
			Type implementationType = node.GetImplementationType();
			InjekGraphNodeKind kind = node.Kind;
			string displayName = BuildDisplayName(node.DisplayName, kind, serviceType, implementationType, node.RequiresReferenceSlot);
			return new InjekkoBindingAuthoringDefinition(
				kind,
				displayName,
				node.ReferenceSlotId,
				serviceType,
				implementationType,
				node.RequiresReferenceSlot,
				node.GetDefaultReference());
		}

		internal static string BuildDisplayName(string explicitDisplayName, InjekGraphNodeKind kind, Type serviceType, Type implementationType, bool requiresReferenceSlot)
		{
			if (!string.IsNullOrWhiteSpace(explicitDisplayName))
				return explicitDisplayName;

			if (requiresReferenceSlot)
				return "Unnamed Reference";

			string serviceName = GetFriendlyTypeName(serviceType);
			string implementationName = GetFriendlyTypeName(implementationType);

			return kind switch
			{
				InjekGraphNodeKind.BindInstance => $"Bind Instance<{serviceName}>",
				InjekGraphNodeKind.BindPrefab => $"Bind Prefab<{serviceName}>",
				InjekGraphNodeKind.BindTransient => $"Bind Transient<{serviceName}>",
				InjekGraphNodeKind.BindScoped => $"Bind Scoped<{serviceName}>",
				InjekGraphNodeKind.BindRedirectTransient => $"Bind Transient<{serviceName} -> {implementationName}>",
				InjekGraphNodeKind.BindRedirectScoped => $"Bind Scoped<{serviceName} -> {implementationName}>",
				InjekGraphNodeKind.CustomInstaller => "Custom Installer",
				_ => kind.ToString(),
			};
		}

		static string GetFriendlyTypeName(Type type)
			=> type == null ? "Unassigned" : type.FullName?.Replace("+", ".") ?? type.Name;
	}

	internal static class InjekkoGraphReflectionUtility
	{
		internal static IReadOnlyList<InjekkoBindingAuthoringDefinition> GetBindingDefinitions(Graph graph)
		{
			if (graph == null)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var contexts = graph.GetNodes()
				.OfType<BindDeclarationContextNode>()
				.ToArray();
			if (contexts.Length == 0)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var definitions = new List<InjekkoBindingAuthoringDefinition>();
			foreach (var context in contexts)
			{
				var orderedBlocks = context.BlockNodes
					.OfType<BindDeclarationBlockNode>()
					.OrderBy(static block => block.Index)
					.ToArray();
				if (orderedBlocks.Length == 0)
					continue;

				var definition = TryCreateDefinitionFromBlocks(context, orderedBlocks);
				if (definition.HasValue)
					definitions.Add(definition.Value);
			}

			return definitions;
		}

		internal static IReadOnlyList<IInjekkoBindingAuthoringNode> GetBindingNodes(Graph graph)
		{
			if (graph == null)
				return Array.Empty<IInjekkoBindingAuthoringNode>();

			return graph.GetNodes()
				.OfType<IInjekkoBindingAuthoringNode>()
				.ToArray();
		}

		static InjekkoBindingAuthoringDefinition? TryCreateDefinitionFromBlocks(BindDeclarationContextNode context, BindDeclarationBlockNode[] blocks)
		{
			if (blocks == null || blocks.Length == 0)
				return null;

			var first = blocks[0];
			Type implementationType = null;
			Type serviceType = null;
			InjekGraphNodeKind kind;
			bool requiresReferenceSlot = false;
			string referenceSlotId = string.Empty;
			string displayName = string.Empty;
			UnityEngine.Object defaultReference = null;

			switch (first)
			{
				case InstanceBlock instanceBlock:
				{
					implementationType = null;
					serviceType = blocks.Length > 1 && blocks[1] is IInjekkoDestinationBlock destinationBlock
						? destinationBlock.GetServiceType(instanceBlock.GetValueType())
						: instanceBlock.GetValueType();
					kind = InjekGraphNodeKind.BindInstance;
					requiresReferenceSlot = true;
					referenceSlotId = instanceBlock.ReferenceSlotId ?? string.Empty;
					displayName = instanceBlock.FieldName ?? string.Empty;
					defaultReference = instanceBlock.GetDefaultReference(instanceBlock.GetValueType());
					break;
				}

				case TypeBlock typeBlock:
					return null;

				default:
					return null;
			}

			displayName = InjekkoGraphToolkitBridge.BuildDisplayName(displayName, kind, serviceType, implementationType, requiresReferenceSlot);
			return new InjekkoBindingAuthoringDefinition(
				kind,
				displayName,
				referenceSlotId,
				serviceType,
				implementationType,
				requiresReferenceSlot,
				defaultReference);
		}
	}
}
