namespace Injekko.Unity
{
	public class InjekkoProjectAsset : ScriptableObject
	{
		public string ProjectName { get; set; } = "InjekkoProject";
		public IReadOnlyList<InjekInstallerAsset> ProjectInstallers { get; init; } = Array.Empty<InjekInstallerAsset>();
		public IReadOnlyList<InjekInstallerAsset> DefaultSceneInstallers { get; init; } = Array.Empty<InjekInstallerAsset>();

		public virtual IReadOnlyList<InjekInstallerAsset> GetProjectInstallers()
			=> ProjectInstallers;

		public virtual IReadOnlyList<InjekInstallerAsset> GetSceneInstallers(Scene scene)
			=> DefaultSceneInstallers;
	}
}
