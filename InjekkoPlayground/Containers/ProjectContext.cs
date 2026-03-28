using Injekko;
using System.Collections.Generic;

public class ProjectContext : Context
{
	public static ProjectContext Create(Scene scene)
	{
		GameObject gameObject = scene.AddNewGameObject("ProjectContext");
		ProjectContext projectContainer = gameObject.AddComponent<ProjectContext>();
		return projectContainer;
	}

	protected override IEnumerable<IInjekInstaller> GetInstallers()
	{
		yield return new ProjectInstaller();
	}
}
