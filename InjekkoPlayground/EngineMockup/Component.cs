public class Component
{
	public GameObject gameObject;
	public virtual void Awake() { }
	public virtual void Start() { }
	public virtual void Update() { }
	public virtual void Destroy() { }
	public T GetComponent<T>() where T : Component, new() => gameObject.GetComponent<T>();
}




