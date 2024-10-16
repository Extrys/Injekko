public class Project
{
	List<Scene> scenes = new();
	private static Scene currentScene;

	public static Scene CurrentScene
	{
		get => currentScene;
		private set
		{
			currentScene?.Unload();
			currentScene = value;
			currentScene.Load();
		}
	}

	public void AddScene(Scene scene)
	{
		scenes.Add(scene);
	}
	public void LoadScene(int index)
	{
		CurrentScene = scenes[index];
	}
	public void UpdateScene()
	{
		CurrentScene.Update();
	}
	public void UnloadScene()
	{
		CurrentScene.Unload();
		currentScene = null;
	}
}




