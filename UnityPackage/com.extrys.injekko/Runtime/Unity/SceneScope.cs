using UnityEngine;

namespace Injekko.Unity
{
	[DefaultExecutionOrder(-32000)]
	[DisallowMultipleComponent]
	public sealed class SceneScope : MonoBehaviour
	{
		[SerializeField] InjekInstallerAsset[] installers = null;

		public InjekScopeNode ScopeNode { get; private set; }
		internal InjekInstallerAsset[] Installers => installers ?? System.Array.Empty<InjekInstallerAsset>();

		void Awake()
		{
			ScopeNode = InjekScopeRegistry.RegisterSceneScope(this);
		}

		internal void AssignScope(InjekScopeNode scopeNode)
		{
			ScopeNode = scopeNode;
		}
	}
}
