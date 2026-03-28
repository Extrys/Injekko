using Injekko;

public class GameObjectContext : Context
{
	public override void EnsureParent()
	{
		if (container.Parent == null)
			container.Parent = gameObject.Scene.FindObjectOfType<SceneContext>().container;
	}
}




