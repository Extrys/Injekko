using Injekko;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateFucktory]
	public class MySuperCustomComponent : MonoBehaviour
	{
		public MySuperCustomClass MySuperCustomClass { get; private set; }

		[Injek]
		public void Construct(MySuperCustomClass mySuperCustomClass)
		{
			MySuperCustomClass = mySuperCustomClass;
			Debug.Log($"MySuperCustomComponent injected: {mySuperCustomClass.Name}");
		}
	}
}
