using Injekko;
using Pepe;

public sealed class ProjectInstaller : IInjekInstaller
{
	readonly DependencyA depA = new("TestA");
	readonly DependencyB depB = new("TestB");
	readonly DependencyC depC = new("TestC");
	readonly VelocityProvider velocityProvider = new();

	public void Install(IInjekBindingBuilder builder)
	{
		builder.BindInstance(depA);
		builder.BindInstance(depB);
		builder.BindInstance(depC);
		builder.BindInstance(velocityProvider);
	}
}
