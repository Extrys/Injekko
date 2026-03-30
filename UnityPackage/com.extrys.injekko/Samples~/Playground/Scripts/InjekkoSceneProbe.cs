using UnityEngine;
using UnityEngine.InputSystem;

namespace Injekko.Samples.Playground
{
	public class InjekkoSceneProbe : MonoBehaviour
	{
		[SerializeField] InputAction spawnInput;

		MonoComponentWithFucktory_Fucktory customComponentFucktory;

		private void Awake()
		{
			spawnInput.performed += OnSpawnInputPerformed;
			spawnInput.Enable();
		}

		[Injek]
		public void Construct(MonoComponentWithFucktory_Fucktory customComponentFucktory, StringArrayScriptable stringsDb)
		{
			this.customComponentFucktory = customComponentFucktory;
			string allStrings = string.Join(", ", stringsDb.strings);
			Debug.Log($"TestPlayerInputInjekko injected. DB assigned with strings: {allStrings}");
		}

		void OnSpawnInputPerformed(InputAction.CallbackContext context)
		{
			Debug.Log("Space pressed, creating MySuperCustomComponent");
			var component = customComponentFucktory.Create();
			Debug.Log($"Created component: {component.NameClass?.Name}");
		}
	}
}
