using Injekko;

namespace Injekko.Unity
{
	public static class InjekScopeExtensions
	{
		public static IInjekScope GetInjekScope(this GameObject gameObject)
			=> InjekScopeRegistry.GetScope(gameObject);

		public static IInjekScope GetInjekScope(this Component component)
			=> InjekScopeRegistry.GetScope(component);
	}
}
