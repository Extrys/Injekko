using Injekko;

public class Context : Component
{
	public InjekkoContainer container = new();
	bool isInitialized;
	public override void Awake()
	{
		Initialize();
	}
	public void Initialize()
	{
		if (isInitialized)
			return;

		EnsureParent();
		InstallBindings();
		isInitialized = true;
	}

	public virtual void EnsureParent()
	{
		
	}
	public virtual void InstallBindings()
	{
		
	}

	public IInjekScope Scope => container;

	public T Resolve<T>() => container.Resolve<T>();
}



