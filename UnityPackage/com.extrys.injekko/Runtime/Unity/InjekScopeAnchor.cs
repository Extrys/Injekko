using UnityEngine;

namespace Injekko.Unity
{
	public sealed class InjekScopeAnchor : MonoBehaviour
	{
		[SerializeField] InjekInstallerAsset[] installers = null;

		public InjekScopeNode ScopeNode { get; private set; }

		void Awake()
		{
			ScopeNode = InjekScopeRegistry.EnsureSubscope(gameObject, installers);
		}
	}
}
