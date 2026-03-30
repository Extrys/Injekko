using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Injekko.Unity;
using Injekko.Editor.GraphToolkit;

namespace Injekko.Editor
{
	[CustomEditor(typeof(InjekCompiledScopePlan))]
	internal sealed class InjekCompiledScopePlanEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var graphAsset = (InjekCompiledScopePlan)target;
			serializedObject.Update();
			DrawDefaultInspector();
			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox("This is the compiled runtime plan generated from the Graph Toolkit asset. The same .injekgraph file is draggable in runtime fields and still editable as a graph.", MessageType.Info);

			DrawAuthoringGraphButtons(graphAsset);
			InjekScopeGraphEditorUtility.DrawBindingSummary(graphAsset);

			EditorGUILayout.Space();
			if (GUILayout.Button("Compile Graph Plans"))
				InjekScopeGraphCompiler.CompileAll();
		}

		static void DrawAuthoringGraphButtons(InjekCompiledScopePlan graphAsset)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Open Authoring Graph"))
					InjekkoGraphToolkitBridge.OpenAuthoringGraph(graphAsset);

				bool hasAuthoringGraph = InjekkoGraphToolkitBridge.HasAuthoringGraph(graphAsset);
				using (new EditorGUI.DisabledScope(!hasAuthoringGraph))
				{
					if (GUILayout.Button("Ping Authoring Graph"))
					{
						string authoringGraphPath = InjekkoGraphToolkitBridge.GetAuthoringGraphAssetPath(graphAsset);
						UnityEngine.Object mainAsset = string.IsNullOrWhiteSpace(authoringGraphPath)
							? null
							: AssetDatabase.LoadMainAssetAtPath(authoringGraphPath);
						if (mainAsset != null)
						{
							EditorGUIUtility.PingObject(mainAsset);
							Selection.activeObject = mainAsset;
						}
					}
				}
			}
		}
	}

	[CustomEditor(typeof(InjekkoProjectAsset))]
	internal sealed class InjekkoProjectAssetEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var projectAsset = (InjekkoProjectAsset)target;
			DrawDefaultInspector();
			InjekScopeGraphEditorLayout.DrawHostGraphControls(
				projectAsset.GraphPlan,
				() =>
				{
					var createdGraph = InjekScopeGraphEditorUtility.CreateGraphAsset($"{projectAsset.name}_ProjectScopeGraph");
					if (createdGraph == null)
						return;

					Undo.RecordObject(projectAsset, "Assign Injek Project Graph");
					projectAsset.SetEditorGraph(createdGraph);
					EditorUtility.SetDirty(projectAsset);
					AssetDatabase.SaveAssets();
					InjekkoGraphToolkitBridge.OpenAuthoringGraph(createdGraph);
				});
			InjekScopeGraphEditorUtility.DrawReferenceBindings(projectAsset, allowSceneObjects: false);

			EditorGUILayout.Space();
			if (GUILayout.Button("Refresh Project Graph Bindings"))
			{
				InjekScopeGraphCompiler.RefreshProjectAssetBindings(projectAsset);
				AssetDatabase.SaveAssets();
			}

			if (GUILayout.Button("Compile Graph Plans"))
				InjekScopeGraphCompiler.CompileAll();
		}
	}

	[CustomEditor(typeof(SceneScope))]
	internal sealed class SceneScopeEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var sceneScope = (SceneScope)target;
			DrawDefaultInspector();
			InjekScopeGraphEditorLayout.DrawHostGraphControls(
				sceneScope.GraphPlan,
				() =>
				{
					string sceneName = sceneScope.gameObject.scene.IsValid()
						? sceneScope.gameObject.scene.name
						: sceneScope.name;
					var createdGraph = InjekScopeGraphEditorUtility.CreateGraphAsset($"{sceneName}_SceneScopeGraph");
					if (createdGraph == null)
						return;

					Undo.RecordObject(sceneScope, "Assign Injek Scene Graph");
					sceneScope.SetEditorGraph(createdGraph);
					EditorUtility.SetDirty(sceneScope);
					if (sceneScope.gameObject.scene.IsValid())
						EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
					AssetDatabase.SaveAssets();
					InjekkoGraphToolkitBridge.OpenAuthoringGraph(createdGraph);
				});
			InjekScopeGraphEditorUtility.DrawReferenceBindings(sceneScope, allowSceneObjects: true);
			InjekScopeGraphEditorUtility.DrawSceneInjectionGraphSection(sceneScope);

			EditorGUILayout.Space();
			if (GUILayout.Button("Refresh Scene Graph Cache"))
			{
				InjekScopeGraphCompiler.RefreshSceneScopeCache(sceneScope);
				if (sceneScope.gameObject.scene.IsValid())
					EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
			}

			if (GUILayout.Button("Compile Graph Plans"))
				InjekScopeGraphCompiler.CompileAll();
		}
	}

	internal static class InjekScopeGraphEditorUtility
	{
		internal static InjekCompiledScopePlan CreateGraphAsset(string defaultName)
		{
			string path = EditorUtility.SaveFilePanelInProject(
				"Create Injek Scope Graph",
				string.IsNullOrWhiteSpace(defaultName) ? "InjekScopeGraph" : defaultName,
				InjekkoAuthoringGraph.AssetExtension,
				"Choose where to create the graph asset.");
			if (string.IsNullOrWhiteSpace(path))
				return null;

			global::Unity.GraphToolkit.Editor.GraphDatabase.CreateGraph<InjekkoAuthoringGraph>(path);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(path);
			return AssetDatabase.LoadAssetAtPath<InjekCompiledScopePlan>(path);
		}

		internal static void DrawReferenceBindings(IInjekGraphReferenceHost host, bool allowSceneObjects)
		{
			if (host?.GraphPlan == null)
				return;

			var definitions = InjekkoGraphToolkitBridge.GetBindingDefinitions(host.GraphPlan)
				.Where(static definition => definition.RequiresReferenceSlot && !string.IsNullOrWhiteSpace(definition.ReferenceSlotId))
				.ToArray();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Graph References", EditorStyles.boldLabel);

			if (definitions.Length == 0)
			{
				EditorGUILayout.HelpBox("The assigned graph currently has no reference-backed nodes.", MessageType.None);
				return;
			}

			var bindings = host is InjekkoProjectAsset projectAsset
				? projectAsset.GraphBindings
				: host is SceneScope sceneScope
					? sceneScope.GraphBindings
					: Array.Empty<InjekGraphReferenceBinding>();

			var updatedBindings = bindings.ToDictionary(static binding => binding.SlotId, static binding => binding.Target, StringComparer.Ordinal);
			bool hasChanged = false;

			foreach (var definition in definitions)
			{
				bool hasLocalOverride = updatedBindings.TryGetValue(definition.ReferenceSlotId, out var localTarget);
				bool shouldUseGraphDefaultAsFieldValue = !allowSceneObjects;
				var effectiveTarget = hasLocalOverride
					? localTarget
					: shouldUseGraphDefaultAsFieldValue
						? definition.DefaultReference
						: null;

				using (new EditorGUILayout.HorizontalScope())
				{
					var nextTarget = EditorGUILayout.ObjectField(
						BuildReferenceLabel(definition, hasLocalOverride, allowSceneObjects),
						effectiveTarget,
						definition.GetExpectedReferenceType(),
						allowSceneObjects);

					using (new EditorGUI.DisabledScope(!hasLocalOverride && definition.DefaultReference == null))
					{
						if (GUILayout.Button("Use Graph", GUILayout.Width(78f)))
						{
							if (updatedBindings.Remove(definition.ReferenceSlotId))
								hasChanged = true;
						}
					}

					if (nextTarget == effectiveTarget)
						continue;

					updatedBindings[definition.ReferenceSlotId] = nextTarget;
					hasChanged = true;
				}
			}

			if (!hasChanged)
				return;

			var serializedBindings = definitions
				.Select(definition =>
				{
					updatedBindings.TryGetValue(definition.ReferenceSlotId, out var target);
					return new InjekGraphReferenceBinding(definition.ReferenceSlotId, target);
				})
				.Where(static binding => binding.Target != null)
				.ToArray();

			if (host is InjekkoProjectAsset writableProjectAsset)
			{
				Undo.RecordObject(writableProjectAsset, "Edit Injek Project Graph Bindings");
				writableProjectAsset.SetEditorGraphBindings(serializedBindings);
				EditorUtility.SetDirty(writableProjectAsset);
			}
			else if (host is SceneScope writableSceneScope)
			{
				Undo.RecordObject(writableSceneScope, "Edit Injek Scene Graph Bindings");
				writableSceneScope.SetEditorGraphBindings(serializedBindings);
				EditorUtility.SetDirty(writableSceneScope);
				if (writableSceneScope.gameObject.scene.IsValid())
					EditorSceneManager.MarkSceneDirty(writableSceneScope.gameObject.scene);
			}
		}

		internal static void DrawBindingSummary(InjekCompiledScopePlan graphAsset)
		{
			var definitions = InjekkoGraphToolkitBridge.GetBindingDefinitions(graphAsset);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Binding Nodes", EditorStyles.boldLabel);

			if (definitions.Count == 0)
			{
				EditorGUILayout.HelpBox("No binding nodes found yet. Open the authoring graph and start adding nodes there.", MessageType.None);
				return;
			}

			foreach (var definition in definitions)
			{
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
				{
					EditorGUILayout.LabelField(definition.DisplayName, EditorStyles.boldLabel);
					EditorGUILayout.LabelField("Bind Type", BuildBindingTypeLabel(definition));
					if (definition.ImplementationType != null)
						EditorGUILayout.LabelField("Implementation", BuildTypeLabel(definition.ImplementationType));
					if (definition.RequiresReferenceSlot)
						EditorGUILayout.LabelField("Reference Slot", definition.ReferenceSlotId);
				}
			}
		}

		internal static void DrawSceneInjectionGraphSection(SceneScope sceneScope)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Generated Scene Injection Graph", EditorStyles.boldLabel);

			var generatedGraph = sceneScope.GeneratedSceneInjectionGraph;
			int nodeCount = generatedGraph?.Nodes?.Length ?? 0;
			if (nodeCount == 0)
			{
				EditorGUILayout.HelpBox("The scene injection graph has not been generated yet. Refresh the scene graph cache or compile graph plans.", MessageType.None);
				return;
			}

			EditorGUILayout.HelpBox("This graph is generated from the current scene structure and is read-only. It represents the cached scope tree and injectable activation plan.", MessageType.Info);
			EditorGUILayout.LabelField("Node Count", nodeCount.ToString());

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Open Generated Graph"))
					InjekSceneInjectionGraphWindow.Open(sceneScope);

				if (GUILayout.Button("Ping SceneScope"))
				{
					EditorGUIUtility.PingObject(sceneScope);
					Selection.activeObject = sceneScope;
				}
			}
		}

		static string BuildReferenceLabel(InjekkoBindingAuthoringDefinition definition, bool hasLocalOverride, bool allowSceneObjects)
		{
			string sourceSuffix = hasLocalOverride
				? string.Empty
				: allowSceneObjects
					? string.Empty
					: " (Graph)";
			return $"{definition.DisplayName} : {BuildBindingTypeLabel(definition)}{sourceSuffix}";
		}

		static string BuildBindingTypeLabel(InjekkoBindingAuthoringDefinition definition)
		{
			if (definition.Kind == InjekGraphNodeKind.CustomInstaller)
				return nameof(InjekInstallerAsset);

			return BuildTypeLabel(definition.ServiceType);
		}

		static string BuildTypeLabel(InjekGraphTypeReference typeReference)
		{
			if (!typeReference.IsAssigned)
				return "Unassigned";

			if (!string.IsNullOrWhiteSpace(typeReference.QualifiedTypeName))
				return typeReference.QualifiedTypeName.Replace("+", ".");

			return "Unassigned";
		}

		static string BuildTypeLabel(Type type)
			=> type == null ? "Unassigned" : (type.FullName?.Replace("+", ".") ?? type.Name);
	}

	static class InjekScopeGraphEditorLayout
	{
		internal static void DrawHostGraphControls(InjekCompiledScopePlan graphAsset, Action createAndAssign)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Authoring Graph", EditorStyles.boldLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(graphAsset == null ? "Create And Assign Graph" : "Open Graph"))
				{
					if (graphAsset == null)
						createAndAssign?.Invoke();
					else
						InjekkoGraphToolkitBridge.OpenAuthoringGraph(graphAsset);
				}

				using (new EditorGUI.DisabledScope(graphAsset == null))
				{
					if (GUILayout.Button("Ping Graph"))
					{
						EditorGUIUtility.PingObject(graphAsset);
						Selection.activeObject = graphAsset;
					}
				}
			}

			if (graphAsset == null)
				EditorGUILayout.HelpBox("Assign or create an Injek scope graph to author bindings visually.", MessageType.Warning);
		}
	}
}
