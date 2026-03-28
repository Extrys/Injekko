using UnityEngine;

namespace Injekko.Unity
{
	public static class InjekDeferredLifecycle
	{
		public static Handle Begin(GameObject gameObject) => Handle.Noop;

		public sealed class Handle : System.IDisposable
		{
			internal static readonly Handle Noop = new();
			public void Complete(Component component) { }
			public void Dispose() { }
		}
	}
}
