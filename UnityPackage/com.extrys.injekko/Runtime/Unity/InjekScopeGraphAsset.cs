using System;
using System.Collections.Generic;
using UnityEngine;

namespace Injekko.Unity
{
	[CreateAssetMenu(menuName = "Injekko/Scope Graph", fileName = "InjekScopeGraph")]
	public sealed class InjekScopeGraphAsset : ScriptableObject
	{
		[SerializeField] string graphName = "InjekScopeGraph";
		[SerializeField, HideInInspector] string graphId = string.Empty;
		[SerializeField, HideInInspector] string authoringGraphAssetGuid = string.Empty;
		[SerializeField, HideInInspector] List<InjekGraphNodeModel> nodes = new();

		public string GraphName => string.IsNullOrWhiteSpace(graphName) ? name : graphName;
		public string GraphId
		{
			get
			{
				EnsureGraphIdentity();
				return graphId;
			}
		}

		public IReadOnlyList<InjekGraphNodeModel> Nodes
		{
			get
			{
				EnsureGraphIdentity();
				// Legacy compatibility only. Graph Toolkit authoring is the main path.
				return nodes;
			}
		}

		public string AuthoringGraphAssetGuid => authoringGraphAssetGuid ?? string.Empty;

		internal InjekGraphNodeModel AddNode(InjekGraphNodeKind kind)
		{
			EnsureGraphIdentity();
			var node = new InjekGraphNodeModel
			{
				Kind = kind,
				Title = kind.ToString()
			};
			node.EnsureIdentifiers();
			nodes.Add(node);
			return node;
		}

		internal void RemoveNode(InjekGraphNodeModel node)
		{
			if (node == null)
				return;

			nodes?.Remove(node);
		}

		void OnValidate()
		{
			EnsureGraphIdentity();
		}

		public void EnsureGraphIdentity()
		{
			if (string.IsNullOrWhiteSpace(graphId))
				graphId = Guid.NewGuid().ToString("N");

			if (nodes == null)
				nodes = new List<InjekGraphNodeModel>();

			foreach (var node in nodes)
				node?.EnsureIdentifiers();
		}

		internal void SetEditorAuthoringGraphGuid(string guid)
		{
			authoringGraphAssetGuid = guid ?? string.Empty;
		}
	}
}
