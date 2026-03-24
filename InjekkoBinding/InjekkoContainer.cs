using System;
using System.Collections.Generic;

namespace Injekko
{
	public class InjekkoContainer : IInjekScope
	{
		public IInjekScope Parent { get; set; }
		readonly Dictionary<Type, object> bindings = new();

		public void Bind<TInterface, TImplementation>() where TImplementation : TInterface, new() => bindings[typeof(TInterface)] = new TImplementation();
		public void BindInstance<TInterface>(TInterface instance) => bindings[typeof(TInterface)] = instance;

		public bool TryResolve<TInterface>(out TInterface instance)
		{
			if (bindings.TryGetValue(typeof(TInterface), out var value))
			{
				instance = (TInterface)value;
				return true;
			}

			if (Parent != null)
				return Parent.TryResolve(out instance);

			instance = default;
			return false;
		}

		public TInterface Resolve<TInterface>()
		{
			if (TryResolve<TInterface>(out var instance))
				return instance;
			throw new InjekException($"No binding found for {typeof(TInterface).FullName}");
		}
	}
}
