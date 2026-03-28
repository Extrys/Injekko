using Injekko.Unity;

public sealed class PlaygroundProjectAsset : InjekkoProjectAsset
{
	public PlaygroundProjectAsset()
	{
		ProjectName = "PlaygroundProject";
		ProjectInstallers = new InjekInstallerAsset[]
		{
			new PlaygroundProjectInstallerAsset(),
		};
	}
}
