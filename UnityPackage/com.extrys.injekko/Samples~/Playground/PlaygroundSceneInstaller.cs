using Injekko;
using Injekko.Unity;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateAssetMenu(fileName = "SCENE_Installer", menuName = "Injekko/Samples/SCENE_Installer")]
	public class PlaygroundSceneInstaller : InjekInstallerAsset
	{
		public MyVFXDB myOverrideDb;

		public override void Install(IInjekBindingBuilder builder)
		{
			Debug.Log("Installing PlaygroundSceneInstaller");
			builder.BindInstance(myOverrideDb);
			builder.BindInstance(new SceneOnlyClass { Name = "Hello Scene Scope" });
		}
	}
}
