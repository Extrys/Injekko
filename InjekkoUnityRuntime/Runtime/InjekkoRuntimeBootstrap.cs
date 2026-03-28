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

		public static void Configure(InjekkoProjectAsset projectAsset)
		{
			ProjectAsset = projectAsset;
			EnsureInitialized();
		}

		public static void EnsureInitialized()
		{
			if (isInitialized)
				return;

			InjekScopeRegistry.Configure(configuredProjectAsset ?? new InjekkoProjectAsset());
			isInitialized = true;
		}
	}
}
