using UnityEngine;

namespace Injekko.Unity
{
	public abstract class InjekInstallerAsset : ScriptableObject, IInjekInstaller
	{
		public abstract void Install(IInjekBindingBuilder builder);
	}
}
