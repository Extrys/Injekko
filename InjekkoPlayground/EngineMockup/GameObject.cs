public class GameObject
{
	string name;
	Scene scene;
	public Scene Scene => scene;
	public List<Component> components = new();

	public string Name { get => name; set => name = value; }

	public GameObject parent;
	public List<GameObject> children = new();

	public GameObject(string name, Scene scene, GameObject parent)
	{
		this.name = name;
		this.scene = scene;
		this.parent = parent;
		parent?.children.Add(this);
	}
	public T AddComponent<T>() where T : Component, new()
	{
		T component = new();
		component.gameObject = this;
		components.Add(component);
		return component;
	}

	public T GetComponent<T>() where T : Component, new()
	{
		foreach (var component in components)
		{
			if (component is T)
				return (T)component;
		}
		return null;
	}
}




