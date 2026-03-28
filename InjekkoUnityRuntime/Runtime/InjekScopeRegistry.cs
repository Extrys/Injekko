namespace Injekko.Unity
{
	public static class InjekScopeRegistry
	{
		static InjekkoProjectAsset projectAsset;
		static InjekScopeNode projectScope;
		static readonly Dictionary<Scene, InjekScopeNode> sceneScopes = new();
		static readonly Dictionary<GameObject, InjekScopeNode> anchoredScopes = new();

		public static void Configure(InjekkoProjectAsset asset)
		{
			projectAsset = asset;
			projectScope = null;
			sceneScopes.Clear();
			anchoredScopes.Clear();
		}

		public static InjekScopeNode GetProjectScope()
		{
			if (projectScope != null)
				return projectScope;

			projectScope = new InjekScopeNode(projectAsset?.ProjectName ?? "InjekkoProject", InjekScopeKind.Project, projectAsset);
			if (projectAsset != null)
				projectScope.Install(projectAsset.GetProjectInstallers());
			return projectScope;
		}

		public static InjekScopeNode EnsureSceneScope(Scene scene)
		{
			if (sceneScopes.TryGetValue(scene, out var scope))
				return scope;

			scope = new InjekScopeNode(scene.Name, InjekScopeKind.Scene, scene, GetProjectScope());
			if (projectAsset != null)
				scope.Install(projectAsset.GetSceneInstallers(scene));
			sceneScopes[scene] = scope;
			return scope;
		}

		public static InjekScopeNode EnsureSubscope(GameObject gameObject, IEnumerable<InjekInstallerAsset> installers = null)
		{
			if (anchoredScopes.TryGetValue(gameObject, out var scope))
				return scope;

			var parentScope = ResolveParentScope(gameObject);
			scope = new InjekScopeNode(gameObject.Name, InjekScopeKind.GameObject, gameObject, parentScope);
			if (installers != null)
				scope.Install(installers);
			anchoredScopes[gameObject] = scope;
			return scope;
		}

		public static InjekScopeNode GetScopeNode(GameObject gameObject)
		{
			if (gameObject == null)
				throw new ArgumentNullException(nameof(gameObject));

			var current = gameObject;
			while (current != null)
			{
				if (anchoredScopes.TryGetValue(current, out var anchoredScope))
					return anchoredScope;

				current = current.parent;
			}

			return EnsureSceneScope(gameObject.Scene);
		}

		public static InjekScopeNode GetScopeNode(Component component)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));

			return GetScopeNode(component.gameObject);
		}

		public static IInjekScope GetScope(GameObject gameObject) => GetScopeNode(gameObject);
		public static IInjekScope GetScope(Component component) => GetScopeNode(component);

		public static IEnumerable<InjekScopeNode> EnumerateScopes()
		{
			if (projectScope == null)
				yield break;

			yield return projectScope;
			foreach (var sceneScope in sceneScopes.Values)
				yield return sceneScope;
			foreach (var anchoredScope in anchoredScopes.Values)
				yield return anchoredScope;
		}

		public static void ReleaseScene(Scene scene)
		{
			sceneScopes.Remove(scene);

			var gameObjectsToRemove = new List<GameObject>();
			foreach (var entry in anchoredScopes)
			{
				if (entry.Key.Scene == scene)
					gameObjectsToRemove.Add(entry.Key);
			}

			foreach (var gameObject in gameObjectsToRemove)
				anchoredScopes.Remove(gameObject);
		}

		static InjekScopeNode ResolveParentScope(GameObject gameObject)
		{
			var current = gameObject.parent;
			while (current != null)
			{
				if (anchoredScopes.TryGetValue(current, out var scope))
					return scope;

				current = current.parent;
			}

			return EnsureSceneScope(gameObject.Scene);
		}
	}
}
