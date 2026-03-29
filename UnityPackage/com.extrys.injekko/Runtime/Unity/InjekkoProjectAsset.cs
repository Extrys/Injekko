using UnityEngine;

namespace Injekko.Unity
{
	[CreateAssetMenu(menuName = "Injekko/Project Asset", fileName = "InjekkoProjectAsset")]
	public class InjekkoProjectAsset : ScriptableObject
	{
		[SerializeField] string projectName = "InjekkoProject";
		[SerializeField] InjekInstallerAsset[] projectInstallers = null;

		public string ProjectName => string.IsNullOrWhiteSpace(projectName) ? "InjekkoProject" : projectName;

		public virtual InjekInstallerAsset[] GetProjectInstallers()
			=> projectInstallers ?? System.Array.Empty<InjekInstallerAsset>();
	}
}
