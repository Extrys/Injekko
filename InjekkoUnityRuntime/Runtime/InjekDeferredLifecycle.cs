namespace Injekko.Unity
{
	public static class InjekDeferredLifecycle
	{
		static readonly Dictionary<GameObject, int> delayedGameObjects = new();

		public static Handle Begin(GameObject gameObject)
		{
			if (gameObject == null || !gameObject.Scene.IsLoaded)
				return Handle.Noop;

			delayedGameObjects.TryGetValue(gameObject, out var count);
			delayedGameObjects[gameObject] = count + 1;
			return new Handle(gameObject, isActive: true);
		}

		internal static bool ShouldDelayLifecycle(GameObject gameObject)
			=> gameObject != null && delayedGameObjects.ContainsKey(gameObject);

		static void Release(GameObject gameObject)
		{
			if (gameObject == null)
				return;

			if (!delayedGameObjects.TryGetValue(gameObject, out var count))
				return;

			if (count <= 1)
			{
				delayedGameObjects.Remove(gameObject);
				return;
			}

			delayedGameObjects[gameObject] = count - 1;
		}

		public sealed class Handle : IDisposable
		{
			internal static readonly Handle Noop = new(null, isActive: false);

			readonly GameObject gameObject;
			readonly bool isActive;
			bool completed;

			internal Handle(GameObject gameObject, bool isActive)
			{
				this.gameObject = gameObject;
				this.isActive = isActive;
			}

			public void Complete(Component component)
			{
				if (!isActive || completed)
					return;

				completed = true;
				Release(gameObject);
				component?.Awake();
				component?.Start();
			}

			public void Dispose()
			{
				if (!isActive || completed)
					return;

				completed = true;
				Release(gameObject);
			}
		}
	}
}
