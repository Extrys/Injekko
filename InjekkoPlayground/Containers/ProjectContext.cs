using Injekko;
using Pepe;

public class ProjectContext : Context
{
	DependencyA depA = new("TestA"); // en vez de esto poner installers, que tengan el metodo de installbindings
	DependencyB depB = new("TestB");
	DependencyC depC = new("TestC");
	VelocityProvider velocityProvider = new();

	public static ProjectContext Create(Scene scene)
	{
		GameObject gameObject = scene.AddNewGameObject("ProjectContext");
		ProjectContext projectContainer = gameObject.AddComponent<ProjectContext>();
		return projectContainer;
	}

	public override void InstallBindings()
	{
		container.BindInstance(depA);
		container.BindInstance(depB);
		container.BindInstance(depC);
		container.BindInstance(velocityProvider);
	}
}
