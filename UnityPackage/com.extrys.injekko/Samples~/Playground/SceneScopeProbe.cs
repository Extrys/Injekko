using Injekko;
using UnityEngine;

namespace Injekko.Samples.Playground
{
	public class SceneScopeProbe : MonoBehaviour
	{
		[Injek]
		public void Construct(SceneOnlyClass sceneOnlyClass, MyVFXDB myVfxDb)
		{
			Debug.Log($"SceneScopeProbe injected. SceneOnlyClass='{sceneOnlyClass.Name}', DB assigned: {myVfxDb != null}");
		}
	}
}
