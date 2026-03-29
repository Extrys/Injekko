using UnityEngine;

namespace Injekko
{
	public interface IInjekBindingBuilder
	{
		void BindInstance<T>(T instance);
		void BindTransient<T>() where T : new();
		void BindTransient<TService, TImplementation>() where TImplementation : TService, new();
		void BindScoped<T>() where T : new();
		void BindScoped<TService, TImplementation>() where TImplementation : TService, new();
		void BindPrefab<T>(T prefab) where T : Component;
	}
}
