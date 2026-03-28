using Injekko;
using Injekko.Unity;
using Pepe;

public sealed class PlaygroundProjectInstallerAsset : InjekInstallerAsset
{
	readonly DependencyA depA = new("TestA");
	readonly DependencyB depB = new("TestB");
	readonly DependencyC depC = new("TestC");
	readonly VelocityProvider velocityProvider = new();

	public override void Install(IInjekBindingBuilder builder)
	{
		builder.BindInstance(depA);
		builder.BindInstance(depB);
		builder.BindInstance(depC);
		builder.BindInstance(velocityProvider);
	}
}
