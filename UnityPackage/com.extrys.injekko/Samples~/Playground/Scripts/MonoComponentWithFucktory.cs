using Injekko;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateFucktory]
	public class MonoComponentWithFucktory : MonoBehaviour
	{
		public InjectableNameClass NameClass { get; private set; }

		[Injek]
		public void Construct(InjectableNameClass nameClass)
		{
			NameClass = nameClass;
			Debug.Log($"NameClass injected with name: {NameClass.Name}");
		}
	}
}
