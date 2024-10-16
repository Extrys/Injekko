using Injekko;

public class GameObjectContext : Context
{
	public override void EnsureParent()
	{
		if (container.Parent == null)
			container.Parent = Project.CurrentScene.FindObjectOfType<SceneContext>().container;
	}
}




