using Injekko.Unity;

public class Program
{
	static readonly Project project = new();

	static void Main(string[] args)
	{
		InjekkoRuntimeBootstrap.Configure(new PlaygroundProjectAsset());
		project.AddScene(CreateDefaultSceneSetup());
		project.LoadScene(0);
		project.UpdateScene();
		project.UnloadScene();
	}

	public static Scene CreateDefaultSceneSetup()
	{
		Scene scene = new();

		GameObject playerObject = scene.AddNewGameObject("Player");
		InjekScopeAnchor playerScopeAnchor = playerObject.AddComponent<InjekScopeAnchor>();
		playerScopeAnchor.Configure();

		Player_Fucktory playerFucktory = new(playerObject);
		playerFucktory.Create();

		scene.AddNewGameObject("Inventory", playerObject);
		scene.LogScene();
		Console.WriteLine("Scene created");
		return scene;
	}
}
