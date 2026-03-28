using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	public static class InjekkoRuntimeBootstrap
	{
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
				configuredProjectAsset = Resources.Load<InjekkoProjectAsset>("InjekkoProjectAsset");

			if (configuredProjectAsset == null)
				configuredProjectAsset = ScriptableObject.CreateInstance<InjekkoProjectAsset>();

			InjekScopeRegistry.Configure(configuredProjectAsset);
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			isInitialized = true;
		}

		static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			InjekScopeRegistry.EnsureSceneScope(scene);
		}

		static void OnSceneUnloaded(Scene scene)
		{
			InjekScopeRegistry.ReleaseScene(scene);
		}
	}
}
