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
			serializedObject.Update();
			var graphProperty = serializedObject.FindProperty("sceneGraph");
			if (graphProperty != null)
				EditorGUILayout.PropertyField(graphProperty, new GUIContent("Scene Graph"));
			serializedObject.ApplyModifiedProperties();

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
			InjekScopeGraphEditorUtility.DrawSceneInjectionGraphSummary(sceneScope);
		}
	}

	internal static class InjekScopeGraphEditorUtility
	{
		const string SceneToolsFoldoutKey = "Injekko.SceneScopeEditor.SceneToolsFoldout";
		static GUIStyle sceneToolsFoldoutStyle;

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

			var bindings = host is InjekkoProjectAsset projectAsset
				? projectAsset.GraphBindings
				: host is SceneScope sceneScope
					? sceneScope.GraphBindings
					: Array.Empty<InjekGraphReferenceBinding>();

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				if (definitions.Length == 0)
				{
					EditorGUILayout.HelpBox("The assigned graph currently has no reference-backed nodes.", MessageType.None);
					return;
				}

				var updatedBindings = bindings.ToDictionary(static binding => binding.SlotId, static binding => binding.Target, StringComparer.Ordinal);
				bool hasChanged = false;

				foreach (var definition in definitions)
				{
					bool hasLocalOverride = updatedBindings.TryGetValue(definition.ReferenceSlotId, out var localTarget);
					var effectiveTarget = hasLocalOverride ? localTarget : definition.DefaultReference;

					using (new EditorGUILayout.HorizontalScope())
					{
						var nextTarget = EditorGUILayout.ObjectField(
							BuildReferenceLabel(definition, hasLocalOverride),
							effectiveTarget,
							definition.GetExpectedReferenceType(),
							allowSceneObjects);

						if (nextTarget == effectiveTarget)
							continue;

						if (nextTarget == null && definition.DefaultReference == null)
						{
							if (updatedBindings.Remove(definition.ReferenceSlotId))
								hasChanged = true;
							continue;
						}

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

		internal static void DrawSceneInjectionGraphSummary(SceneScope sceneScope)
		{
			EditorGUILayout.Space();

			var generatedGraph = sceneScope.GeneratedSceneInjectionGraph;
			int nodeCount = generatedGraph?.Nodes?.Length ?? 0;
			bool isExpanded = SessionState.GetBool(SceneToolsFoldoutKey, true);
			isExpanded = EditorGUILayout.Foldout(isExpanded, "Scene Tools", true, GetSceneToolsFoldoutStyle());
			SessionState.SetBool(SceneToolsFoldoutKey, isExpanded);
			if (!isExpanded)
				return;

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField("Scene Node Count", nodeCount.ToString());

				using (new EditorGUILayout.HorizontalScope())
				{
					using (new EditorGUI.DisabledScope(nodeCount == 0))
					{
						if (GUILayout.Button("Open Generated Graph"))
							InjekSceneInjectionGraphWindow.Open(sceneScope);
					}

					if (GUILayout.Button("Refresh Scene Graph Cache"))
					{
						InjekScopeGraphCompiler.RefreshSceneScopeCache(sceneScope);
						if (sceneScope.gameObject.scene.IsValid())
							EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
					}
				}

				if (GUILayout.Button("Recompile Plans"))
					InjekScopeGraphCompiler.CompileAll();
			}
		}

		static string BuildReferenceLabel(InjekkoBindingAuthoringDefinition definition, bool hasLocalOverride)
		{
			string sourceSuffix = !hasLocalOverride && definition.DefaultReference != null
				? " [Graph Default]"
				: string.Empty;
			return $"{definition.DisplayName}{sourceSuffix}";
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

		static GUIStyle GetSceneToolsFoldoutStyle()
		{
			if (sceneToolsFoldoutStyle != null)
				return sceneToolsFoldoutStyle;

			sceneToolsFoldoutStyle = new GUIStyle(EditorStyles.foldout);
			Color textColor = EditorStyles.label.normal.textColor;
			textColor.a = EditorGUIUtility.isProSkin ? 0.4f : 0.5f;

			sceneToolsFoldoutStyle.normal.textColor = textColor;
			sceneToolsFoldoutStyle.onNormal.textColor = textColor;
			sceneToolsFoldoutStyle.hover.textColor = textColor;
			sceneToolsFoldoutStyle.onHover.textColor = textColor;
			sceneToolsFoldoutStyle.focused.textColor = textColor;
			sceneToolsFoldoutStyle.onFocused.textColor = textColor;

			return sceneToolsFoldoutStyle;
		}
	}

	static class InjekScopeGraphEditorLayout
	{
		internal static void DrawHostGraphControls(InjekCompiledScopePlan graphAsset, Action createAndAssign)
		{
			if (graphAsset == null)
			{
				EditorGUILayout.Space();
				EditorGUILayout.HelpBox("Assign or create an Injek scope graph to author bindings visually.", MessageType.Warning);
				if (GUILayout.Button("Create And Assign Graph"))
					createAndAssign?.Invoke();
			}
		}
	}
}
