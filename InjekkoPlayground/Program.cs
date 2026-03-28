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
		GameObject sceneContextObject = scene.AddNewGameObject("SceneContext");
		SceneContext sceneContext = sceneContextObject.AddComponent<SceneContext>();
		sceneContext.Initialize();

		GameObject playerObject = scene.AddNewGameObject("Player");
		GameObjectContext playerContext = playerObject.AddComponent<GameObjectContext>();
		playerContext.Initialize();

		Player_Fucktory playerFucktory = new(playerObject, playerContext.Scope);
		playerFucktory.Create();

		scene.AddNewGameObject("Inventory", playerObject);
		scene.LogScene();
		Console.WriteLine("Scene created");
		return scene;
	}
}




