using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using Injekko.Unity;

namespace Injekko.Editor.GraphToolkit
{
	internal static class InjekkoGraphImportLowering
	{
		internal static IReadOnlyList<InjekkoBindingAuthoringDefinition> LowerBindingDefinitions(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var graph = GraphDatabase.LoadGraphForImporter<InjekkoAuthoringGraph>(assetPath);
			if (graph == null)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var contexts = graph.GetNodes().OfType<BindDeclarationContextNode>().ToArray();
			if (contexts.Length == 0)
				return Array.Empty<InjekkoBindingAuthoringDefinition>();

			var definitions = new List<InjekkoBindingAuthoringDefinition>();
			foreach (var context in contexts)
			{
				var blocks = context.BlockNodes.OfType<BindDeclarationBlockNode>().OrderBy(static block => block.Index).ToArray();
				var definition = TryCreateDefinition(blocks);
				if (definition.HasValue)
					definitions.Add(definition.Value);
			}

			return definitions;
		}

		static InjekkoBindingAuthoringDefinition? TryCreateDefinition(BindDeclarationBlockNode[] blocks)
		{
			if (blocks == null || blocks.Length == 0 || blocks[0] is not InstanceBlock instanceBlock)
				return null;

			Type serviceType = blocks.Length > 1 && blocks[1] is IInjekkoDestinationBlock destinationBlock
				? destinationBlock.GetServiceType(instanceBlock.GetValueType())
				: instanceBlock.GetValueType();
			string displayName = InjekkoGraphToolkitBridge.BuildDisplayName(instanceBlock.FieldName, InjekGraphNodeKind.BindInstance, serviceType, requiresReferenceSlot: true);
			return new InjekkoBindingAuthoringDefinition(
				InjekGraphNodeKind.BindInstance,
				displayName,
				instanceBlock.ReferenceSlotId ?? string.Empty,
				serviceType,
				null,
				requiresReferenceSlot: true,
				instanceBlock.GetDefaultReference(instanceBlock.GetValueType()));
		}
	}
}
