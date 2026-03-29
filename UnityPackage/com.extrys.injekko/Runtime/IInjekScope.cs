using UnityEngine;

namespace Injekko
{
	public interface IInjekScope
	{
		IInjekScope Parent { get; }
		bool TryResolve<T>(out T instance);
		T Resolve<T>();
		bool TryResolvePrefab<T>(out T prefab) where T : Component;
		T ResolvePrefab<T>() where T : Component;
		bool TryBeginActivation(object instance);
		void CompleteActivation(object instance);
		void CancelActivation(object instance);
		bool IsActivated(object instance);
	}
}
