using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Injekko.Editor.GraphToolkit;
using Injekko.Unity;

namespace Injekko.Editor
{
	internal static class InjekScopeHostBindingSynchronizer
	{
		internal static void RefreshSceneScopeBindings(SceneScope sceneScope)
		{
			if (sceneScope == null)
				return;

			var bindings = BuildReferenceBindings(sceneScope.GraphPlan, sceneScope.GraphBindings);
			if (AreBindingsEqual(sceneScope.GraphBindings, bindings))
				return;

			sceneScope.SetEditorGraphBindings(bindings);
			EditorUtility.SetDirty(sceneScope);
			if (sceneScope.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(sceneScope.gameObject.scene);
		}

		internal static void RefreshGameObjectScopeBindings(GameObjectScope gameObjectScope)
		{
			if (gameObjectScope == null)
				return;

			var bindings = BuildReferenceBindings(gameObjectScope.GraphPlan, gameObjectScope.GraphBindings);
			if (AreBindingsEqual(gameObjectScope.GraphBindings, bindings))
				return;

			gameObjectScope.SetEditorGraphBindings(bindings);
			EditorUtility.SetDirty(gameObjectScope);
			if (gameObjectScope.gameObject.scene.IsValid())
				EditorSceneManager.MarkSceneDirty(gameObjectScope.gameObject.scene);
		}

		static InjekGraphReferenceBinding[] BuildReferenceBindings(InjekCompiledScopePlan graphPlan, IReadOnlyList<InjekGraphReferenceBinding> existingBindings)
		{
			if (graphPlan == null)
				return Array.Empty<InjekGraphReferenceBinding>();

			// The imported plan is the only IR here; we never re-lower the authoring graph in editor sync.
			var existingBySlot = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
			foreach (var binding in existingBindings ?? Array.Empty<InjekGraphReferenceBinding>())
			{
				if (binding == null || string.IsNullOrWhiteSpace(binding.SlotId) || existingBySlot.ContainsKey(binding.SlotId))
					continue;

				existingBySlot.Add(binding.SlotId, binding.Target);
			}

			var bindings = new List<InjekGraphReferenceBinding>();
			foreach (var definition in graphPlan.BindingDefinitions.Select(InjekkoBindingAuthoringDefinition.FromCompiledDefinition))
			{
				if (!definition.RequiresReferenceSlot || string.IsNullOrWhiteSpace(definition.ReferenceSlotId))
					continue;

				existingBySlot.TryGetValue(definition.ReferenceSlotId, out var target);
				if (target != null)
					bindings.Add(new InjekGraphReferenceBinding(definition.ReferenceSlotId, target));
			}

			return bindings.ToArray();
		}

		static bool AreBindingsEqual(IReadOnlyList<InjekGraphReferenceBinding> current, IReadOnlyList<InjekGraphReferenceBinding> next)
		{
			current ??= Array.Empty<InjekGraphReferenceBinding>();
			next ??= Array.Empty<InjekGraphReferenceBinding>();
			if (current.Count != next.Count)
				return false;

			for (int index = 0; index < current.Count; index++)
			{
				var currentBinding = current[index];
				var nextBinding = next[index];
				if (!string.Equals(currentBinding?.SlotId ?? string.Empty, nextBinding?.SlotId ?? string.Empty, StringComparison.Ordinal))
					return false;
				if (!ReferenceEquals(currentBinding?.Target, nextBinding?.Target))
					return false;
			}

			return true;
		}
	}
}
