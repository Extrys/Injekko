namespace Injekko
{
	public interface IInjekScope
	{
		IInjekScope Parent { get; }
		bool TryResolve<T>(out T instance);
		T Resolve<T>();
		bool TryBeginActivation(object instance);
		void CompleteActivation(object instance);
		void CancelActivation(object instance);
		bool IsActivated(object instance);
	}
}
