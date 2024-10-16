using System.Text;

public class Scene 
{
	public string Name { get; private set; }
	public Scene(string name = "UNNAMED")
	{
		Name = name;
	}

	List<GameObject> rootGameObjects = new();

	public T FindObjectOfType<T>() where T : Component
	{
		foreach (GameObject gameObject in rootGameObjects)
		{
			foreach (Component component in gameObject.components)
			{
				if (component is T)
					return component as T;
			}
		}
		return null;
	}

	public GameObject AddNewGameObject(string name, GameObject parent = null)
	{
		GameObject gameObject = new(name, this, parent);
		if (parent == null)
			rootGameObjects.Add(gameObject);
		return gameObject;
	}
	public void Load()
	{
		Console.WriteLine($"Loading Scene: {Name}");
		for (int i = 0; i < rootGameObjects.Count; i++)
		{
			GameObject gameObject = rootGameObjects[i];
			for (int j = 0; j < gameObject.components.Count;j++)
				gameObject.components[j].Awake();

			for (int j = 0; j < gameObject.components.Count; j++)
				gameObject.components[j].Start();

			for (int j = 0; j < gameObject.children.Count; j++)
			{
				GameObject child = gameObject.children[j];
				for (int k = 0; k < child.components.Count; k++)
					child.components[k].Awake();
				for (int k = 0; k < child.components.Count; k++)
					child.components[k].Start();
			}
		}
	}
	public void Update()
	{
		foreach (GameObject gameObject in rootGameObjects)
		{
			foreach (Component component in gameObject.components)
				component.Update();
			foreach (GameObject child in gameObject.children)
			{
				foreach (Component component in child.components)
					component.Update();
			}
		}
	}
	public void Unload()
	{
		foreach (GameObject gameObject in rootGameObjects)
		{
			foreach (Component component in gameObject.components)
				component.Destroy();
			foreach (GameObject child in gameObject.children)
			{
				foreach (Component component in child.components)
					component.Destroy();
			}
		}
	}

	public void LogScene()
	{
		StringBuilder log = new();
		log.AppendLine($"Scene: {Name}");
		for (int i = 0; i < rootGameObjects.Count; i++)
		{
			LogGameObject(rootGameObjects[i], 1, log);
		}
		File.WriteAllText(@".\Logs\SceneLog.txt", log.ToString());
	}
	void LogGameObject(GameObject go, int depth, StringBuilder log)
	{
		string tabs = new('\t', depth);
		log.AppendLine($"{tabs}{go.Name}(GO): ");
		foreach (Component component in go.components)
		{
			log.AppendLine($"{tabs}\t| {component.GetType().Name} ");
		}
		log.AppendLine();
		foreach (GameObject child in go.children)
			LogGameObject(child, depth + 1, log);
	}
}




