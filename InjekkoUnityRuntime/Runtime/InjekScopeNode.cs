using Injekko;

namespace Injekko.Unity
{
	public sealed class InjekScopeNode : IInjekScope, IInjekBindingBuilder
	{
		readonly InjekkoContainer container = new();
		readonly List<InjekScopeNode> children = new();

		public InjekScopeNode(string name, InjekScopeKind kind, object owner, InjekScopeNode parent = null)
		{
			Name = name;
			Kind = kind;
			Owner = owner;
			ParentNode = parent;
			container.Parent = parent;
			parent?.children.Add(this);
		}

		public string Name { get; }
		public InjekScopeKind Kind { get; }
		public object Owner { get; }
		public InjekScopeNode ParentNode { get; }
		public IReadOnlyList<InjekScopeNode> Children => children;
		public IReadOnlyList<InjekBindingInfo> Bindings => container.GetBindingInfos();

		public IInjekScope Parent => container.Parent;

		public void Install(IInjekInstaller installer)
			=> installer?.Install(this);

		public void Install(IEnumerable<IInjekInstaller> installers)
		{
			if (installers == null)
				return;

			foreach (var installer in installers)
				installer?.Install(this);
		}

		public void BindInstance<T>(T instance) => container.BindInstance(instance);
		public void BindTransient<T>() where T : new() => container.BindTransient<T>();
		public void BindTransient<TService, TImplementation>() where TImplementation : TService, new() => container.BindTransient<TService, TImplementation>();
		public void BindScoped<T>() where T : new() => container.BindScoped<T>();
		public void BindScoped<TService, TImplementation>() where TImplementation : TService, new() => container.BindScoped<TService, TImplementation>();
		public bool TryResolve<T>(out T instance) => container.TryResolve(out instance);
		public T Resolve<T>() => container.Resolve<T>();
		public bool TryBeginActivation(object instance) => container.TryBeginActivation(instance);
		public void CompleteActivation(object instance) => container.CompleteActivation(instance);
		public void CancelActivation(object instance) => container.CancelActivation(instance);
		public bool IsActivated(object instance) => container.IsActivated(instance);
	}
}
