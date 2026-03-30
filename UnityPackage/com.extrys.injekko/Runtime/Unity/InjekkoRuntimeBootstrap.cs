using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekkoRuntimeBootstrap
	{
		const string ProjectResourceName = "InjekkoProjectAsset";

		static bool isInitialized;
		static InjekkoProjectAsset configuredProjectAsset;

		public static InjekkoProjectAsset ProjectAsset
		{
			get => configuredProjectAsset;
			set
			{
				configuredProjectAsset = value;
				isInitialized = false;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void AutoInitialize()
		{
			EnsureInitialized();
		}

		public static void Configure(InjekkoProjectAsset projectAsset)
		{
			ProjectAsset = projectAsset;
			EnsureInitialized();
		}

		public static void EnsureInitialized()
		{
			if (isInitialized)
				return;

			if (configuredProjectAsset == null)
			{
				var directProjectGraph = Resources.Load<InjekCompiledScopePlan>(ProjectResourceName);
				if (directProjectGraph != null)
					configuredProjectAsset = InjekkoProjectAsset.CreateRuntimeProjectAsset(directProjectGraph, directProjectGraph.name);
			}

			if (configuredProjectAsset == null)
				configuredProjectAsset = Resources.Load<InjekkoProjectAsset>(ProjectResourceName);

			if (configuredProjectAsset == null)
				configuredProjectAsset = ScriptableObject.CreateInstance<InjekkoProjectAsset>();

			InjekScopeRegistry.Configure(configuredProjectAsset);
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
