using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateAssetMenu(fileName = "MyVFXDB", menuName = "Injekko/Samples/MyVFXDB")]
	public class MyVFXDB : ScriptableObject
	{
		public string[] vfxNames;
	}
}
