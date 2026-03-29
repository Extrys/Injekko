using Injekko;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	public class TestPlayerInputInjekko : MonoBehaviour
	{
		MySuperCustomComponent_Fucktory customComponentFucktory;
		MyVFXDB myVfxDb;

		[Injek]
		public void Construct(MySuperCustomComponent_Fucktory customComponentFucktory, MyVFXDB myVfxDb)
		{
			this.customComponentFucktory = customComponentFucktory;
			this.myVfxDb = myVfxDb;
			Debug.Log($"TestPlayerInputInjekko injected. DB assigned: {myVfxDb != null}");
		}

		void Update()
		{
			if (!Input.GetKeyDown(KeyCode.Space))
				return;

			Debug.Log("Space pressed, creating MySuperCustomComponent");
			var component = customComponentFucktory.Create();
			Debug.Log($"Created component: {component.MySuperCustomClass?.Name}");
		}
	}
}
