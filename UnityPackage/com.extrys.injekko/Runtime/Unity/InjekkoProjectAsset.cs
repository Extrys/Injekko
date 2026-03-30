using UnityEngine;

namespace Injekko.Unity
{
	[CreateAssetMenu(menuName = "Injekko/Project Asset", fileName = "InjekkoProjectAsset")]
	public class InjekkoProjectAsset : ScriptableObject, IInjekGraphReferenceHost
	{
		[SerializeField] string projectName = "InjekkoProject";
		[SerializeField] InjekCompiledScopePlan projectGraph = null;
		[SerializeField, HideInInspector] InjekGraphReferenceBinding[] projectBindings = null;

		public string ProjectName => string.IsNullOrWhiteSpace(projectName) ? "InjekkoProject" : projectName;
		public InjekCompiledScopePlan GraphPlan => projectGraph;
		public string GraphId => projectGraph != null ? projectGraph.GraphId : string.Empty;
		public InjekGraphReferenceBinding[] GraphBindings => projectBindings ?? System.Array.Empty<InjekGraphReferenceBinding>();

		public UnityEngine.Object GetGraphReferenceOrNull(string slotId)
		{
			if (string.IsNullOrWhiteSpace(slotId))
				return null;

			foreach (var binding in GraphBindings)
			{
				if (binding != null && binding.SlotId == slotId && binding.Target != null)
					return binding.Target;
			}

			return projectGraph != null ? projectGraph.GetDefaultReferenceOrNull(slotId) : null;
		}

		public UnityEngine.Object GetGraphReferenceOrThrow(string slotId)
		{
			var reference = GetGraphReferenceOrNull(slotId);
			if (reference != null)
				return reference;

			throw new InjekException($"Project graph reference '{slotId}' was not assigned on {name}.");
		}

		internal void SetEditorGraph(InjekCompiledScopePlan graphAsset)
		{
			projectGraph = graphAsset;
		}

		internal void SetEditorGraphBindings(InjekGraphReferenceBinding[] bindings)
		{
			projectBindings = bindings;
		}

		internal static InjekkoProjectAsset CreateRuntimeProjectAsset(InjekCompiledScopePlan graphAsset, string runtimeProjectName = "InjekkoProject")
		{
			var runtimeAsset = CreateInstance<InjekkoProjectAsset>();
			runtimeAsset.hideFlags = HideFlags.HideAndDontSave;
			runtimeAsset.projectName = string.IsNullOrWhiteSpace(runtimeProjectName) ? "InjekkoProject" : runtimeProjectName;
			runtimeAsset.projectGraph = graphAsset;
			runtimeAsset.projectBindings = System.Array.Empty<InjekGraphReferenceBinding>();
			return runtimeAsset;
		}
	}
}
