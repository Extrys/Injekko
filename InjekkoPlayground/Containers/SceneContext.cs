public class SceneContext : Context
{
	public override void EnsureParent()
	{
		if(container.Parent == null)
		{
			ProjectContext context = ProjectContext.Create(gameObject.Scene);
			container.Parent = context.container;
			context.Initialize();
		}
	}
}



