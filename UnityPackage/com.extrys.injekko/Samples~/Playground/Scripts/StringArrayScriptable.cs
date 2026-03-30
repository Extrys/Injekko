using UnityEngine;

namespace Injekko.Samples.Playground
{
	[CreateAssetMenu(fileName = "StringArrayScriptable", menuName = "Injekko/Samples/StringArrayScriptable")]
	public class StringArrayScriptable : ScriptableObject
	{
		public string[] strings;
	}
}
