using Injekko;
using Injekko.Unity;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateAssetMenu(fileName = "PROJECT_Installer", menuName = "Injekko/Samples/PROJECT_Installer")]
	public class PlaygroundProjectInstaller : InjekInstallerAsset
	{
		public MyVFXDB myVfxDb;

		public override void Install(IInjekBindingBuilder builder)
		{
			Debug.Log("Installing PlaygroundProjectInstaller");
			builder.BindInstance(new MySuperCustomClass { Name = "Hello Project Scope" });
			builder.BindInstance(myVfxDb);
		}
	}

	public class MySuperCustomClass
	{
		public string Name { get; set; }
	}

	public class SceneOnlyClass
	{
		public string Name { get; set; }
	}
}
