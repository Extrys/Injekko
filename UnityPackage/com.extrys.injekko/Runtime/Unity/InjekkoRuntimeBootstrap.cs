using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekkoRuntimeBootstrap
	{
		static bool isInitialized;
		static InjekCompiledScopePlan configuredProjectGraph;

		public static InjekCompiledScopePlan ProjectGraph
		{
			get => configuredProjectGraph;
			set
			{
				configuredProjectGraph = value;
				isInitialized = false;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void AutoInitialize()
		{
			EnsureInitialized();
		}

		public static void Configure(InjekCompiledScopePlan projectGraph)
		{
			ProjectGraph = projectGraph;
			EnsureInitialized();
		}

		public static void EnsureInitialized()
		{
			if (isInitialized)
				return;

			// Project graphs are loaded directly as compiled plans; there is no wrapper asset in the main path anymore.
			configuredProjectGraph ??= Resources.Load<InjekCompiledScopePlan>(InjekkoProjectConventions.ProjectPlanResourceName);
			InjekScopeRegistry.Configure(configuredProjectGraph);
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			isInitialized = true;
		}

		static void OnSceneUnloaded(Scene scene)
		{
			InjekScopeRegistry.ReleaseScene(scene);
		}
	}
}
