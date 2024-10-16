public class Program
{
	static Project project = new();
	static void Main(string[] args)
	{
		project.AddScene(CreateDefaultSceneSetup());
		project.LoadScene(0);
		project.UpdateScene();
		project.UnloadScene();
	}

	public static Scene CreateDefaultSceneSetup()
	{
		Scene scene = new();
		GameObject sceneContext = scene.AddNewGameObject("SceneContext");
		sceneContext.AddComponent<SceneContext>();
		GameObject player = scene.AddNewGameObject("Player");
		player.AddComponent<GameObjectContext>();
		player.AddComponent<Player>();
		scene.AddNewGameObject("Inventory", player);
		scene.LogScene();
		Console.WriteLine("Scene created");
		return scene;
	}
}




