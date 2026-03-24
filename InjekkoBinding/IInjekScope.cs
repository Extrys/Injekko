namespace Injekko
{
	public interface IInjekScope
	{
		IInjekScope Parent { get; }
		bool TryResolve<T>(out T instance);
		T Resolve<T>();
	}
}
