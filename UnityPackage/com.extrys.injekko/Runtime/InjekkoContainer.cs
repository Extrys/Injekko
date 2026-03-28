using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Injekko
{
	public sealed class InjekkoContainer : IInjekScope, IInjekBindingBuilder
	{
		IInjekScope parent;
		InjekActivationTracker activationTracker = new();
		readonly Dictionary<Type, IInjekBinding> bindings = new();

		public IInjekScope Parent
		{
			get => parent;
			set
			{
				parent = value;
				if (value is InjekkoContainer parentContainer)
					activationTracker = parentContainer.activationTracker;
			}
		}

		public void BindInstance<T>(T instance) => bindings[typeof(T)] = new InstanceBinding<T>(instance);
		public void BindTransient<T>() where T : new() => BindTransient<T, T>();
		public void BindTransient<TService, TImplementation>() where TImplementation : TService, new() => bindings[typeof(TService)] = new TransientBinding<TService>(() => new TImplementation());
		public void BindScoped<T>() where T : new() => BindScoped<T, T>();
		public void BindScoped<TService, TImplementation>() where TImplementation : TService, new() => bindings[typeof(TService)] = new ScopedBinding<TService>(() => new TImplementation());

		public bool TryResolve<T>(out T instance)
		{
			if (bindings.TryGetValue(typeof(T), out var binding))
			{
				instance = (T)binding.Resolve();
				return true;
			}

			if (Parent != null)
				return Parent.TryResolve(out instance);

			instance = default;
			return false;
		}

		public T Resolve<T>()
		{
			if (TryResolve<T>(out var instance))
				return instance;
			throw new InjekException($"No binding found for {typeof(T).FullName}");
		}

		public bool TryBeginActivation(object instance) => activationTracker.TryBegin(instance);
		public void CompleteActivation(object instance) => activationTracker.Complete(instance);
		public void CancelActivation(object instance) => activationTracker.Cancel(instance);
		public bool IsActivated(object instance) => activationTracker.IsActivated(instance);

		public IReadOnlyList<InjekBindingInfo> GetBindingInfos()
		{
			List<InjekBindingInfo> infos = new(bindings.Count);
			foreach (var entry in bindings)
				infos.Add(new InjekBindingInfo(entry.Key.FullName ?? entry.Key.Name, entry.Value.Lifetime));
			return infos;
		}

		interface IInjekBinding
		{
			object Resolve();
			InjekLifetime Lifetime { get; }
		}

		sealed class InstanceBinding<T> : IInjekBinding
		{
			readonly T instance;
			public InstanceBinding(T instance) { this.instance = instance; }
			public object Resolve() => instance;
			public InjekLifetime Lifetime => InjekLifetime.Instance;
		}

		sealed class TransientBinding<T> : IInjekBinding
		{
			readonly Func<T> factory;
			public TransientBinding(Func<T> factory) { this.factory = factory; }
			public object Resolve() => factory();
			public InjekLifetime Lifetime => InjekLifetime.Transient;
		}

		sealed class ScopedBinding<T> : IInjekBinding
		{
			readonly Func<T> factory;
			bool hasValue;
			T cachedValue;
			public ScopedBinding(Func<T> factory) { this.factory = factory; }
			public object Resolve()
			{
				if (!hasValue)
				{
					cachedValue = factory();
					hasValue = true;
				}
				return cachedValue;
			}
			public InjekLifetime Lifetime => InjekLifetime.Scoped;
		}

		sealed class InjekActivationTracker
		{
			readonly HashSet<object> activated = new(ReferenceEqualityComparer.Instance);
			readonly HashSet<object> activating = new(ReferenceEqualityComparer.Instance);
			public bool TryBegin(object instance)
			{
				if (instance == null)
					return false;
				if (activated.Contains(instance) || activating.Contains(instance))
					return false;
				activating.Add(instance);
				return true;
			}
			public void Complete(object instance)
			{
				if (instance == null)
					return;
				activating.Remove(instance);
				activated.Add(instance);
			}
			public void Cancel(object instance)
			{
				if (instance == null)
					return;
				activating.Remove(instance);
			}
			public bool IsActivated(object instance) => instance != null && activated.Contains(instance);
		}

		sealed class ReferenceEqualityComparer : IEqualityComparer<object>
		{
			public static readonly ReferenceEqualityComparer Instance = new();
			public new bool Equals(object x, object y) => ReferenceEquals(x, y);
			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}
}
