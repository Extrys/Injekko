using Injekko;

public class Context : Component
{
	public InjekkoContainer container = new();
	public override void Awake()
	{
		Initialize();
	}
	public void Initialize()
	{
		EnsureParent();
		InstallBindings();
	}

	public virtual void EnsureParent()
	{
		
	}
	public virtual void InstallBindings()
	{
		
	}

	public T Resolve<T>() => container.Resolve<T>();
}



