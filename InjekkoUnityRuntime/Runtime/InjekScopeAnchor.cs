namespace Injekko.Unity
{
	public class InjekScopeAnchor : Component
	{
		InjekInstallerAsset[] installers = Array.Empty<InjekInstallerAsset>();

		public IReadOnlyList<InjekInstallerAsset> Installers => installers;
		public InjekScopeNode ScopeNode { get; private set; }

		public void Configure(params InjekInstallerAsset[] installers)
		{
			this.installers = installers ?? Array.Empty<InjekInstallerAsset>();
			if (gameObject != null)
				ScopeNode = InjekScopeRegistry.EnsureSubscope(gameObject, this.installers);
		}

		public override void Awake()
		{
			ScopeNode = InjekScopeRegistry.EnsureSubscope(gameObject, installers);
		}
	}
}
