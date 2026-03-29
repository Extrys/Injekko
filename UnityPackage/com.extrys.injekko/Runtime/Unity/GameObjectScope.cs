using UnityEngine;

namespace Injekko.Unity
{
	[DisallowMultipleComponent]
	public sealed class GameObjectScope : MonoBehaviour
	{
		[SerializeField] InjekInstallerAsset[] installers = null;

		public InjekScopeNode ScopeNode { get; private set; }

		void Awake()
		{
			ScopeNode = InjekScopeRegistry.EnsureGameObjectScope(gameObject, installers);
		}
	}
}
