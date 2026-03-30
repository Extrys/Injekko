using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Injekko.Unity;

namespace Injekko.Editor
{
	internal sealed class InjekSceneInjectionGraphWindow : EditorWindow
	{
		SceneScope sceneScope;
		Vector2 scrollPosition;
		readonly Dictionary<string, bool> expandedNodes = new(StringComparer.Ordinal);

		internal static void Open(SceneScope sceneScope)
		{
			if (sceneScope == null)
				return;

			var window = GetWindow<InjekSceneInjectionGraphWindow>("Injek Scene Graph");
			window.sceneScope = sceneScope;
			window.minSize = new Vector2(420f, 320f);
			window.Show();
		}

		void OnGUI()
		{
			if (sceneScope == null)
			{
				EditorGUILayout.HelpBox("Select a SceneScope to inspect its generated scene injection graph.", MessageType.Info);
				return;
			}

			var graph = sceneScope.GeneratedSceneInjectionGraph;
			var nodes = graph?.Nodes ?? Array.Empty<InjekSceneInjectionNode>();
			if (nodes.Length == 0)
			{
				EditorGUILayout.HelpBox("This SceneScope does not have a generated scene injection graph yet. Refresh the scene graph cache first.", MessageType.Info);
				return;
			}

			EditorGUILayout.LabelField(sceneScope.name, EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Scene", string.IsNullOrWhiteSpace(graph.ScenePath) ? "<unsaved scene>" : graph.ScenePath);
			EditorGUILayout.Space();

			var nodesByParent = nodes
				.GroupBy(static node => node.ParentNodeId ?? string.Empty, StringComparer.Ordinal)
				.ToDictionary(static group => group.Key, static group => group.OrderBy(static node => node.DisplayName, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			if (nodesByParent.TryGetValue(string.Empty, out var rootNodes))
			{
				foreach (var rootNode in rootNodes)
					DrawNodeRecursive(rootNode, nodesByParent, 0);
			}
			EditorGUILayout.EndScrollView();
		}

		void DrawNodeRecursive(
			InjekSceneInjectionNode node,
			IReadOnlyDictionary<string, InjekSceneInjectionNode[]> nodesByParent,
			int depth)
		{
			bool hasChildren = nodesByParent.TryGetValue(node.NodeId, out var children) && children.Length > 0;
			string label = BuildLabel(node);

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(depth * 16f);

			if (hasChildren)
			{
				bool isExpanded = expandedNodes.TryGetValue(node.NodeId, out var expanded) && expanded;
				bool nextExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
				if (nextExpanded != isExpanded)
					expandedNodes[node.NodeId] = nextExpanded;
			}
			else
			{
				EditorGUILayout.LabelField(label);
			}

			using (new EditorGUI.DisabledScope(node.TargetComponent == null && node.TargetGameObject == null))
			{
				if (GUILayout.Button("Ping", GUILayout.Width(54f)))
				{
					UnityEngine.Object target = node.TargetComponent != null
						? node.TargetComponent
						: (UnityEngine.Object)node.TargetGameObject;
					EditorGUIUtility.PingObject(target);
					Selection.activeObject = target;
				}
			}

			EditorGUILayout.EndHorizontal();

			if (!hasChildren)
				return;

			bool showChildren = expandedNodes.TryGetValue(node.NodeId, out var childExpanded) && childExpanded;
			if (!showChildren)
				return;

			foreach (var child in children)
				DrawNodeRecursive(child, nodesByParent, depth + 1);
		}

		static string BuildLabel(InjekSceneInjectionNode node)
		{
			string prefix = node.Kind switch
			{
				InjekSceneInjectionNodeKind.SceneScope => "[SceneScope]",
				InjekSceneInjectionNodeKind.GameObjectScope => "[GameObjectScope]",
				InjekSceneInjectionNodeKind.Injectable => "[Injek]",
				_ => "[Node]",
			};

			if (string.IsNullOrWhiteSpace(node.HierarchyPath))
				return $"{prefix} {node.DisplayName}";

			return $"{prefix} {node.DisplayName} - {node.HierarchyPath}";
		}
	}
}
