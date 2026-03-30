using System;
using UnityEngine;

namespace Injekko.Unity
{
	public sealed class InjekCompiledScopePlan : ScriptableObject
	{
		[SerializeField, HideInInspector] string graphId = string.Empty;
		[SerializeField, HideInInspector] string graphName = string.Empty;
		[SerializeField, HideInInspector] InjekCompiledBindingDefinition[] bindingDefinitions = null;

		public string GraphId => graphId ?? string.Empty;
		public string GraphName => string.IsNullOrWhiteSpace(graphName) ? name : graphName;
		public InjekCompiledBindingDefinition[] BindingDefinitions => bindingDefinitions ?? Array.Empty<InjekCompiledBindingDefinition>();

		public UnityEngine.Object GetDefaultReferenceOrNull(string slotId)
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

		internal void SetImportedData(string graphId, string graphName, InjekCompiledBindingDefinition[] bindingDefinitions)
		{
			this.graphId = graphId ?? string.Empty;
			this.graphName = graphName ?? string.Empty;
			this.bindingDefinitions = bindingDefinitions ?? Array.Empty<InjekCompiledBindingDefinition>();
		}
	}
}
