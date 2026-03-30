using System;
using UnityEngine;

namespace Injekko.Unity
{
	public sealed class InjekCompiledScopePlan : ScriptableObject, IInjekGraphReferenceHost
	{
		[SerializeField, HideInInspector] string graphId = string.Empty;
		[SerializeField, HideInInspector] string graphName = string.Empty;
		[SerializeField, HideInInspector] InjekCompiledBindingDefinition[] bindingDefinitions = null;

		public InjekCompiledScopePlan GraphPlan => this;
		public string GraphId => graphId ?? string.Empty;
		public string GraphName => string.IsNullOrWhiteSpace(graphName) ? name : graphName;
		public InjekCompiledBindingDefinition[] BindingDefinitions => bindingDefinitions ?? Array.Empty<InjekCompiledBindingDefinition>();

		public UnityEngine.Object GetGraphReferenceOrNull(string slotId)
		{
			if (string.IsNullOrWhiteSpace(slotId))
				return null;

			foreach (var bindingDefinition in BindingDefinitions)
			{
				if (bindingDefinition != null && bindingDefinition.ReferenceSlotId == slotId)
					return bindingDefinition.DefaultReference;
			}

			return null;
		}

		public UnityEngine.Object GetGraphReferenceOrThrow(string slotId)
		{
			var reference = GetGraphReferenceOrNull(slotId);
			if (reference != null)
				return reference;

			throw new InjekException($"Project graph reference '{slotId}' was not assigned on '{GraphName}'.");
		}

		public UnityEngine.Object GetDefaultReferenceOrNull(string slotId)
			=> GetGraphReferenceOrNull(slotId);

		internal void SetImportedData(string graphId, string graphName, InjekCompiledBindingDefinition[] bindingDefinitions)
		{
			this.graphId = graphId ?? string.Empty;
			this.graphName = graphName ?? string.Empty;
			this.bindingDefinitions = bindingDefinitions ?? Array.Empty<InjekCompiledBindingDefinition>();
		}
	}
}
