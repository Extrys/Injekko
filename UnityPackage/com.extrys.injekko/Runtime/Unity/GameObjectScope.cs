using UnityEngine;

namespace Injekko.Unity
{
	[DisallowMultipleComponent]
	public sealed class GameObjectScope : MonoBehaviour, IInjekScopeHost
	{
		[SerializeField] InjekInstallerAsset[] installers = null;

		public InjekScopeNode ScopeNode { get; private set; }
		internal InjekInstallerAsset[] Installers => installers ?? System.Array.Empty<InjekInstallerAsset>();

		void Awake()
		{
			ScopeNode = InjekScopeRegistry.EnsureGameObjectScope(gameObject, installers);
		}

		internal void AssignScope(InjekScopeNode scopeNode)
		{
			ScopeNode = scopeNode;
		}
	}
}
