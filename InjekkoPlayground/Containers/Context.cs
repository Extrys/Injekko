using Injekko;
using System.Collections.Generic;

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
		InstallBindings(container);
		isInitialized = true;
	}

	public virtual void EnsureParent()
	{
	}

	public virtual void InstallBindings(IInjekBindingBuilder builder)
	{
		foreach (var installer in GetInstallers())
			installer.Install(builder);
	}

	protected virtual IEnumerable<IInjekInstaller> GetInstallers()
	{
		yield break;
	}

	public IInjekScope Scope => container;

	public T Resolve<T>() => container.Resolve<T>();
}
