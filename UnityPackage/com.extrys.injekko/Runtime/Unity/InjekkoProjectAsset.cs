using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Injekko.Unity
{
	[CreateAssetMenu(menuName = "Injekko/Project Asset", fileName = "InjekkoProjectAsset")]
	public class InjekkoProjectAsset : ScriptableObject
	{
		[SerializeField] string projectName = "InjekkoProject";
		[SerializeField] InjekInstallerAsset[] projectInstallers = null;
		[SerializeField] InjekInstallerAsset[] defaultSceneInstallers = null;

		public string ProjectName => string.IsNullOrWhiteSpace(projectName) ? "InjekkoProject" : projectName;

		public virtual IReadOnlyList<InjekInstallerAsset> GetProjectInstallers()
			=> projectInstallers ?? System.Array.Empty<InjekInstallerAsset>();

		public virtual IReadOnlyList<InjekInstallerAsset> GetSceneInstallers(Scene scene)
			=> defaultSceneInstallers ?? System.Array.Empty<InjekInstallerAsset>();
	}
}
