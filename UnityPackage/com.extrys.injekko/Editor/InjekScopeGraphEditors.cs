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
			{
				InjekScopeGraphEditorUtility.DrawGraphSection(
					graphProperty,
					sceneScope,
					"Scene Graph",
					useBox: true,
					createAndAssign: () =>
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
			}
			serializedObject.ApplyModifiedProperties();
			InjekScopeGraphEditorUtility.DrawSceneInjectionGraphSummary(sceneScope);
		}
	}

	[CustomEditor(typeof(GameObjectScope))]
	internal sealed class GameObjectScopeEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var gameObjectScope = (GameObjectScope)target;
			serializedObject.Update();
			var graphProperty = serializedObject.FindProperty("graph");
			if (graphProperty != null)
			{
				InjekScopeGraphEditorUtility.DrawGraphSection(
					graphProperty,
					gameObjectScope,
					"Graph",
					useBox: true,
					createAndAssign: () =>
					{
						string objectName = string.IsNullOrWhiteSpace(gameObjectScope.name)
							? "GameObjectScope"
							: gameObjectScope.name;
						var createdGraph = InjekScopeGraphEditorUtility.CreateGraphAsset($"{objectName}_Graph");
						if (createdGraph == null)
							return;

						Undo.RecordObject(gameObjectScope, "Assign Injek GameObject Graph");
						gameObjectScope.SetEditorGraph(createdGraph);
						EditorUtility.SetDirty(gameObjectScope);
						if (gameObjectScope.gameObject.scene.IsValid())
							EditorSceneManager.MarkSceneDirty(gameObjectScope.gameObject.scene);
						AssetDatabase.SaveAssets();
						InjekkoGraphToolkitBridge.OpenAuthoringGraph(createdGraph);
					});
			}
			serializedObject.ApplyModifiedProperties();
		}
	}

	internal static class InjekScopeGraphEditorUtility
	{
		const string SceneToolsFoldoutKey = "Injekko.SceneScopeEditor.SceneToolsFoldout";
		static GUIStyle sceneToolsFoldoutStyle;
		static GUIStyle sceneGraphTitleStyle;

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

		internal static void DrawGraphSection(SerializedProperty graphProperty, IInjekGraphReferenceHost host, string title, bool useBox, Action createAndAssign)
		{
			if (graphProperty == null || host == null)
				return;

			EditorGUILayout.Space(2f);
			if (useBox)
			{
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
					DrawGraphSectionContent(graphProperty, host, title, createAndAssign, drawInsideExistingBox: true);
				return;
			}

			DrawGraphSectionContent(graphProperty, host, title, createAndAssign, drawInsideExistingBox: false);
		}

		static void DrawGraphSectionContent(SerializedProperty graphProperty, IInjekGraphReferenceHost host, string title, Action createAndAssign, bool drawInsideExistingBox)
		{
			DrawGraphHeaderRow(graphProperty, title);
			if (host.GraphPlan == null)
			{
				EditorGUILayout.Space(4f);
				EditorGUILayout.HelpBox("Assign or create an Injek scope graph to author bindings visually.", MessageType.Warning);
				if (GUILayout.Button("Create And Assign Graph"))
					createAndAssign?.Invoke();
				return;
			}

			DrawReferenceBindings(host, allowSceneObjects: true, drawInsideExistingBox: drawInsideExistingBox, wrapInBox: true);
		}

		internal static void DrawReferenceBindings(IInjekGraphReferenceHost host, bool allowSceneObjects)
			=> DrawReferenceBindings(host, allowSceneObjects, drawInsideExistingBox: false, wrapInBox: true);

		static void DrawReferenceBindings(IInjekGraphReferenceHost host, bool allowSceneObjects, bool drawInsideExistingBox, bool wrapInBox)
		{
			if (host?.GraphPlan == null)
				return;

			var definitions = InjekkoGraphToolkitBridge.GetBindingDefinitions(host.GraphPlan)
				.Where(static definition => definition.RequiresReferenceSlot && !string.IsNullOrWhiteSpace(definition.ReferenceSlotId))
				.ToArray();

			if (!drawInsideExistingBox)
				EditorGUILayout.Space();

			var bindings = host is InjekkoProjectAsset projectAsset
				? projectAsset.GraphBindings
				: host is SceneScope sceneScope
					? sceneScope.GraphBindings
					: host is GameObjectScope gameObjectScope
						? gameObjectScope.GraphBindings
					: Array.Empty<InjekGraphReferenceBinding>();

			Action drawContent = () =>
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
				else if (host is GameObjectScope writableGameObjectScope)
				{
					Undo.RecordObject(writableGameObjectScope, "Edit Injek GameObject Graph Bindings");
					writableGameObjectScope.SetEditorGraphBindings(serializedBindings);
					EditorUtility.SetDirty(writableGameObjectScope);
					if (writableGameObjectScope.gameObject.scene.IsValid())
						EditorSceneManager.MarkSceneDirty(writableGameObjectScope.gameObject.scene);
				}
			};

			if (drawInsideExistingBox)
			{
				EditorGUILayout.Space(4f);
				drawContent();
			}
			else
			{
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
					drawContent();
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
			=> BuildTypeLabel(definition.ServiceType);

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

		static GUIStyle GetSceneGraphTitleStyle()
		{
			if (sceneGraphTitleStyle != null)
				return sceneGraphTitleStyle;

			sceneGraphTitleStyle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = EditorStyles.label.fontSize + 1
			};

			return sceneGraphTitleStyle;
		}

		static void DrawGraphHeaderRow(SerializedProperty graphProperty, string title)
		{
			var currentGraph = graphProperty.objectReferenceValue as InjekCompiledScopePlan;
			Rect rowRect = GUILayoutUtility.GetRect(10f, 32f, GUILayout.ExpandWidth(true));
			Rect contentRect = new Rect(rowRect.x + 8f, rowRect.y + 3f, rowRect.width - 16f, EditorGUIUtility.singleLineHeight);
			Rect labelRect = new Rect(contentRect.x, contentRect.y, 88f, contentRect.height);
			float fieldX = labelRect.xMax + 8f;
			Rect fieldRect = new Rect(fieldX, contentRect.y, Mathf.Max(0f, contentRect.xMax - fieldX), contentRect.height);

			Color previousColor = GUI.contentColor;
			GUI.contentColor = EditorGUIUtility.isProSkin
				? new Color(1f, 1f, 1f, 0.92f)
				: new Color(1f, 1f, 1f, 0.98f);
			EditorGUI.LabelField(labelRect, title, GetSceneGraphTitleStyle());
			GUI.contentColor = previousColor;

			EditorGUI.BeginChangeCheck();
			var nextGraph = (InjekCompiledScopePlan)EditorGUI.ObjectField(
				fieldRect,
				GUIContent.none,
				currentGraph,
				typeof(InjekCompiledScopePlan),
				allowSceneObjects: false);
			if (EditorGUI.EndChangeCheck())
				graphProperty.objectReferenceValue = nextGraph;

			if (Event.current.type == EventType.Repaint)
			{
				Rect separatorRect = new Rect(contentRect.x, fieldRect.yMax + 5f, contentRect.width, 2f);
				EditorGUI.DrawRect(separatorRect, GetSceneGraphSeparatorColor(currentGraph != null));
			}
		}

		static Color GetSceneGraphSeparatorColor(bool hasGraph)
		{
			if (hasGraph)
				return EditorGUIUtility.isProSkin
					? new Color(0.20f, 0.47f, 0.62f, 1f)
					: new Color(0.36f, 0.60f, 0.78f, 1f);

			return EditorGUIUtility.isProSkin
				? new Color(0.55f, 0.39f, 0.14f, 1f)
				: new Color(0.80f, 0.58f, 0.24f, 1f);
		}
	}

	static class InjekScopeGraphEditorLayout
	{
		internal static void DrawHostGraphControls(InjekCompiledScopePlan graphAsset, Action createAndAssign)
		{
			if (graphAsset == null)
			{
				EditorGUILayout.Space();
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
				{
					EditorGUILayout.HelpBox("Assign or create an Injek scope graph to author bindings visually.", MessageType.Warning);
					if (GUILayout.Button("Create And Assign Graph"))
						createAndAssign?.Invoke();
				}
			}
		}
	}
}
